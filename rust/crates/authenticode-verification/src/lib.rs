// Dosya Yolu: REPO_ROOT/rust/crates/authenticode-verification/src/lib.rs
// Amac: Artifact Authenticode trust, publisher subject ve certificate pinini dogrulamak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Windows WinTrust ile zincir/revocation denetimi ve CryptoAPI signer kimligi kontrolu
// Bagimli Oldugu Katman: Tool | Service

#![deny(unsafe_op_in_unsafe_fn)]

use std::{error::Error, fmt, path::Path};

use turkuaz_installer_domain::artifacts::{
    ArtifactSignatureAlgorithm, ArtifactSignatureDescriptor,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AuthenticodeVerificationError {
    UnsupportedPlatform,
    UnsupportedAlgorithm,
    ArtifactMissing,
    InvalidSignature,
    PublisherMismatch,
    CertificatePinMismatch,
    SignerIdentityUnavailable,
}

impl fmt::Display for AuthenticodeVerificationError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(match self {
            Self::UnsupportedPlatform => "Authenticode verification requires Windows.",
            Self::UnsupportedAlgorithm => "Artifact signature algorithm is not supported.",
            Self::ArtifactMissing => "Artifact file does not exist.",
            Self::InvalidSignature => "Authenticode signature or trust chain is invalid.",
            Self::PublisherMismatch => "Authenticode publisher does not match signed release metadata.",
            Self::CertificatePinMismatch => {
                "Authenticode certificate SHA-256 pin does not match signed release metadata."
            }
            Self::SignerIdentityUnavailable => "Authenticode signer certificate could not be read.",
        })
    }
}

impl Error for AuthenticodeVerificationError {}

/// @dev-id TIZ-AUTH-010
/// @purpose Verify the staged artifact's Windows Authenticode signature and pinned publisher.
/// @reason A valid manifest signature alone does not establish trust in downloaded executable bytes.
/// @contract Checks Windows trust/revocation, publisher subject, and an optional certificate SHA-256 pin.
/// @depends WinVerifyTrust and CryptoAPI; caller remains responsible for artifact digest verification.
pub fn verify_authenticode(
    artifact_path: &Path,
    expected_signature: &ArtifactSignatureDescriptor,
) -> Result<(), AuthenticodeVerificationError> {
    if expected_signature.algorithm() != ArtifactSignatureAlgorithm::Authenticode {
        return Err(AuthenticodeVerificationError::UnsupportedAlgorithm);
    }
    if !artifact_path.is_file() {
        return Err(AuthenticodeVerificationError::ArtifactMissing);
    }

    #[cfg(windows)]
    {
        windows_authenticode::verify(artifact_path, expected_signature)
    }

    #[cfg(not(windows))]
    {
        let _ = artifact_path;
        let _ = expected_signature;
        Err(AuthenticodeVerificationError::UnsupportedPlatform)
    }
}

#[cfg(windows)]
mod windows_authenticode {
    use std::{
        ffi::c_void,
        mem::{size_of, zeroed},
        os::windows::ffi::OsStrExt,
        path::Path,
        ptr,
        slice,
    };

    use sha2::{Digest, Sha256};
    use windows_sys::Win32::{
        Foundation::HWND,
        Security::{
            Cryptography::{
                CERT_CONTEXT, CERT_INFO, CERT_NAME_STR_REVERSE_FLAG,
                CERT_X500_NAME_STR, CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                CERT_QUERY_FORMAT_FLAG_BINARY, CERT_QUERY_OBJECT_FILE, CERT_NAME_BLOB,
                CMSG_SIGNER_COUNT_PARAM, CMSG_SIGNER_INFO, CMSG_SIGNER_INFO_PARAM,
                CertCloseStore, CertEnumCertificatesInStore, CertFreeCertificateContext,
                CertNameToStrW, CryptMsgClose, CryptMsgGetParam, CryptQueryObject,
                PKCS_7_ASN_ENCODING, X509_ASN_ENCODING,
            },
            WinTrust::{
                WINTRUST_DATA, WINTRUST_DATA_0,
                WINTRUST_FILE_INFO, WinVerifyTrust, WTD_CHOICE_FILE, WTD_DISABLE_MD2_MD4,
                WTD_REVOKE_WHOLECHAIN, WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT,
                WTD_STATEACTION_IGNORE, WTD_UI_NONE,
            },
        },
    };

    use crate::AuthenticodeVerificationError;

    const MAX_SIGNER_INFO_BYTES: u32 = 1_048_576;
    const GENERIC_VERIFY_V2: windows_sys::core::GUID = windows_sys::core::GUID {\n        data1: 0x00aa_c56b,\n        data2: 0xcd44,\n        data3: 0x11d0,\n        data4: [0x8c, 0xc2, 0x00, 0xc0, 0x4f, 0xc2, 0x95, 0xee],\n    };

    struct QueriedMessage {
        store: *mut c_void,
        message: *mut c_void,
    }

    impl Drop for QueriedMessage {
        fn drop(&mut self) {
            unsafe {
                // @dev-id TIZ-AUTH-011
                // @purpose Release CryptoAPI handles returned by CryptQueryObject.
                // @reason Certificate stores and message handles are owned resources and must not leak.
                if !self.message.is_null() {
                    let _ = CryptMsgClose(self.message);
                }
                if !self.store.is_null() {
                    let _ = CertCloseStore(self.store, 0);
                }
            }
        }
    }

    struct CertificateContext(*const CERT_CONTEXT);

    impl Drop for CertificateContext {
        fn drop(&mut self) {
            if !self.0.is_null() {
                unsafe {
                    // SAFETY: CertEnumCertificatesInStore returns an owned context unless passed
                    // back as the previous item in a subsequent enumeration call.
                    let _ = CertFreeCertificateContext(self.0);
                }
            }
        }
    }

    pub(super) fn verify(
        artifact_path: &Path,
        expected_signature: &crate::ArtifactSignatureDescriptor,
    ) -> Result<(), AuthenticodeVerificationError> {
        let full_path = artifact_path
            .canonicalize()
            .map_err(|_| AuthenticodeVerificationError::ArtifactMissing)?;
        let mut file_path: Vec<u16> = full_path.as_os_str().encode_wide().collect();
        file_path.push(0);

        let trust_status = verify_file_trust(&mut file_path);
        if trust_status != 0 {
            return Err(AuthenticodeVerificationError::InvalidSignature);
        }

        let (subject, certificate_sha256) = signer_identity(&mut file_path)?;
        if !subject.eq_ignore_ascii_case(expected_signature.publisher_subject()) {
            return Err(AuthenticodeVerificationError::PublisherMismatch);
        }
        if expected_signature
            .certificate_sha256()
            .is_some_and(|pin| pin != certificate_sha256)
        {
            return Err(AuthenticodeVerificationError::CertificatePinMismatch);
        }
        Ok(())
    }

    fn verify_file_trust(file_path: &mut [u16]) -> i32 {
        let mut file_info = WINTRUST_FILE_INFO {
            cbStruct: size_of::<WINTRUST_FILE_INFO>() as u32,
            pcwszFilePath: file_path.as_mut_ptr(),
            hFile: ptr::null_mut(),
            pgKnownSubject: ptr::null_mut(),
        };
        let mut trust_data = WINTRUST_DATA {
            cbStruct: size_of::<WINTRUST_DATA>() as u32,
            pPolicyCallbackData: ptr::null_mut(),
            pSIPClientData: ptr::null_mut(),
            dwUIChoice: WTD_UI_NONE,
            fdwRevocationChecks: WTD_REVOKE_WHOLECHAIN,
            dwUnionChoice: WTD_CHOICE_FILE,
            Anonymous: WINTRUST_DATA_0 {
                pFile: &mut file_info,
            },
            dwStateAction: WTD_STATEACTION_IGNORE,
            hWVTStateData: ptr::null_mut(),
            pwszURLReference: ptr::null_mut(),
            dwProvFlags: WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT | WTD_DISABLE_MD2_MD4,
            dwUIContext: 0,
            pSignatureSettings: ptr::null_mut(),
        };
        let mut action = GENERIC_VERIFY_V2;
        unsafe {
            // SAFETY: all pointers reference stack-owned structures and a live UTF-16 path
            // for the duration of the synchronous WinVerifyTrust call.
            WinVerifyTrust(
                ptr::null_mut::<c_void>() as HWND,
                &mut action,
                &mut trust_data,
            )
        }
    }

    fn signer_identity(
        file_path: &mut [u16],
    ) -> Result<(String, String), AuthenticodeVerificationError> {
        let mut store = ptr::null_mut();
        let mut message = ptr::null_mut();
        let mut encoding = 0_u32;
        let mut content_type = 0_u32;
        let mut format_type = 0_u32;
        let query_succeeded = unsafe {
            // SAFETY: file_path is nul-terminated, output pointers are valid, and returned handles
            // are immediately owned by QueriedMessage.
            CryptQueryObject(
                CERT_QUERY_OBJECT_FILE,
                file_path.as_ptr().cast(),
                CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                CERT_QUERY_FORMAT_FLAG_BINARY,
                0,
                &mut encoding,
                &mut content_type,
                &mut format_type,
                &mut store,
                &mut message,
                ptr::null_mut(),
            )
        };
        if query_succeeded == 0 || store.is_null() || message.is_null() {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }
        let handles = QueriedMessage { store, message };

        let mut signer_count = 0_u32;
        let mut count_size = size_of::<u32>() as u32;\n        let count_read = unsafe {
            // SAFETY: message is a live handle from CryptQueryObject; signer_count is writable.
            CryptMsgGetParam(
                handles.message,
                CMSG_SIGNER_COUNT_PARAM,
                0,
                (&mut signer_count as *mut u32).cast(),
                &mut count_size,
            )
        };
        if count_read == 0 || signer_count != 1 {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }

        let mut signer_info_size = 0_u32;
        let size_read = unsafe {
            // SAFETY: null data requests the required size for one signer-info structure.
            CryptMsgGetParam(
                handles.message,
                CMSG_SIGNER_INFO_PARAM,
                0,
                ptr::null_mut(),
                &mut signer_info_size,
            )
        };
        if size_read == 0 || signer_info_size == 0 || signer_info_size > MAX_SIGNER_INFO_BYTES {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }

        let mut signer_info_buffer = vec![0_u8; signer_info_size as usize];
        let info_read = unsafe {
            // SAFETY: signer_info_buffer has the exact size requested from CryptMsgGetParam.
            CryptMsgGetParam(
                handles.message,
                CMSG_SIGNER_INFO_PARAM,
                0,
                signer_info_buffer.as_mut_ptr().cast(),
                &mut signer_info_size,
            )
        };
        if info_read == 0 || signer_info_size as usize > signer_info_buffer.len() {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }
        let signer_info = unsafe {
            // SAFETY: the API returned at least the size of CMSG_SIGNER_INFO in aligned storage.
            ptr::read_unaligned(signer_info_buffer.as_ptr().cast::<CMSG_SIGNER_INFO>())
        };

        let mut cert_info: CERT_INFO = unsafe {
            // SAFETY: CERT_INFO is a plain C structure; zero initializes optional fields.
            zeroed()
        };
        cert_info.Issuer = signer_info.Issuer;
        cert_info.SerialNumber = signer_info.SerialNumber;
        let certificate = find_signer_certificate(handles.store, &cert_info)?;
        let cert_context = unsafe { certificate.0.as_ref() }
            .ok_or(AuthenticodeVerificationError::SignerIdentityUnavailable)?;
        let cert_info = unsafe { cert_context.pCertInfo.as_ref() }
            .ok_or(AuthenticodeVerificationError::SignerIdentityUnavailable)?;

        let subject_len = unsafe {
            // SAFETY: cert_info is owned by the live certificate context.
            CertNameToStrW(
                X509_ASN_ENCODING,
                &cert_info.Subject as *const CERT_NAME_BLOB,
                CERT_X500_NAME_STR | CERT_NAME_STR_REVERSE_FLAG,
                ptr::null_mut(),
                0,
            )
        };
        if subject_len < 2 || subject_len > 16_384 {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }
        let mut subject_buffer = vec![0_u16; subject_len as usize];
        let subject_written = unsafe {
            // SAFETY: subject_buffer is sized from the length returned by CertNameToStrW.
            CertNameToStrW(
                X509_ASN_ENCODING,
                &cert_info.Subject,
                CERT_X500_NAME_STR | CERT_NAME_STR_REVERSE_FLAG,
                subject_buffer.as_mut_ptr(),
                subject_len,
            )
        };
        if subject_written == 0 || subject_written > subject_len {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }
        let subject_end = subject_buffer
            .iter()
            .position(|character| *character == 0)
            .unwrap_or(subject_buffer.len());
        let subject = String::from_utf16(&subject_buffer[..subject_end])
            .map_err(|_| AuthenticodeVerificationError::SignerIdentityUnavailable)?;

        if cert_context.pbCertEncoded.is_null() || cert_context.cbCertEncoded == 0 {
            return Err(AuthenticodeVerificationError::SignerIdentityUnavailable);
        }
        let certificate_bytes = unsafe {
            // SAFETY: the certificate context owns cbCertEncoded bytes at pbCertEncoded.
            slice::from_raw_parts(
                cert_context.pbCertEncoded,
                cert_context.cbCertEncoded as usize,
            )
        };
        let digest = Sha256::digest(certificate_bytes);
        let pin = digest.iter().map(|byte| format!("{byte:02x}")).collect();
        Ok((subject, pin))
    }

    fn find_signer_certificate(
        store: *mut c_void,
        signer_info: &CERT_INFO,
    ) -> Result<CertificateContext, AuthenticodeVerificationError> {
        let mut current = ptr::null();
        loop {
            current = unsafe {
                // SAFETY: store is live. Passing the prior context transfers it back to the
                // enumerator, which releases it as it returns the next context.
                CertEnumCertificatesInStore(store, current)
            };
            if current.is_null() {
                break;
            }
            let context = unsafe { &*current };
            let Some(info) = (unsafe { context.pCertInfo.as_ref() }) else {
                continue;
            };
            if blobs_equal(&info.Issuer, &signer_info.Issuer)
                && blobs_equal(&info.SerialNumber, &signer_info.SerialNumber)
            {
                return Ok(CertificateContext(current));
            }
        }
        Err(AuthenticodeVerificationError::SignerIdentityUnavailable)
    }

    fn blobs_equal(left: &windows_sys::Win32::Security::Cryptography::CRYPT_INTEGER_BLOB, right: &windows_sys::Win32::Security::Cryptography::CRYPT_INTEGER_BLOB) -> bool {
        if left.cbData != right.cbData || left.cbData == 0 || left.pbData.is_null() || right.pbData.is_null() {
            return false;
        }
        unsafe {
            // SAFETY: CryptoAPI structures expose each blob's buffer for cbData bytes.
            slice::from_raw_parts(left.pbData, left.cbData as usize)
                == slice::from_raw_parts(right.pbData, right.cbData as usize)
        }
    }
}

// Dosya Yolu: REPO_ROOT/rust/crates/manifest-verification/src/lib.rs
// Amac: Detached CMS imzasini ve package-scoped signer trust policy'sini dogrulamak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Windows CryptoAPI imzayi signature-only dogrular, sertifika validity, subject ve SHA-256 pinini denetler
// Bagimli Oldugu Katman: Tool | Model

#![deny(unsafe_op_in_unsafe_fn)]

use std::fmt;

use turkuaz_installer_domain::manifest_trust_policy::ManifestTrustPolicy;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ManifestVerificationError {
    EmptyContent,
    EmptySignature,
    InvalidSignature,
    InvalidSignerCertificate,
    SignerCertificateExpired,
    PublisherSubjectMismatch,
    CertificatePinMismatch,
    UnsupportedPlatform,
}

impl fmt::Display for ManifestVerificationError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(match self {
            Self::EmptyContent => "Manifest content is empty.",
            Self::EmptySignature => "Detached manifest signature is missing.",
            Self::InvalidSignature => "Detached manifest signature is invalid.",
            Self::InvalidSignerCertificate => "Manifest signer certificate is invalid.",
            Self::SignerCertificateExpired => {
                "Manifest signer certificate is outside its validity period."
            }
            Self::PublisherSubjectMismatch => {
                "Manifest signer subject does not match the package trust policy."
            }
            Self::CertificatePinMismatch => {
                "Manifest signer certificate SHA-256 pin does not match the package trust policy."
            }
            Self::UnsupportedPlatform => "CMS manifest verification is supported only on Windows.",
        })
    }
}

impl std::error::Error for ManifestVerificationError {}

/// Verifies detached CMS content before any YAML parsing.
///
/// The caller must first enforce manifest and signature byte limits. This function
/// verifies the signer cryptographic signature without building a certificate trust
/// chain, then checks the package's out-of-band subject and certificate pin.
#[cfg(windows)]
pub fn verify_detached_manifest(
    manifest_content: &[u8],
    detached_signature: &[u8],
    trust_policy: &ManifestTrustPolicy,
) -> Result<(), ManifestVerificationError> {
    windows_crypto::verify_detached_manifest(manifest_content, detached_signature, trust_policy)
}

/// Non-Windows builds can compile the shared workspace, but cannot verify Windows CMS.
#[cfg(not(windows))]
pub fn verify_detached_manifest(
    _manifest_content: &[u8],
    _detached_signature: &[u8],
    _trust_policy: &ManifestTrustPolicy,
) -> Result<(), ManifestVerificationError> {
    Err(ManifestVerificationError::UnsupportedPlatform)
}

#[cfg(windows)]
mod windows_crypto {
    use std::{mem::size_of, ptr, slice};

    use sha2::{Digest, Sha256};
    use turkuaz_installer_domain::manifest_trust_policy::ManifestTrustPolicy;
    use windows::Win32::Security::Cryptography::{
        CERT_CONTEXT, CERT_NAME_STR_REVERSE_FLAG, CERT_X500_NAME_STR, CRYPT_VERIFY_MESSAGE_PARA,
        CertFreeCertificateContext, CertNameToStrW, CertVerifyTimeValidity,
        CryptGetMessageSignerCount, CryptVerifyDetachedMessageSignature, PKCS_7_ASN_ENCODING,
        X509_ASN_ENCODING,
    };

    use crate::ManifestVerificationError;

    struct SignerCertificate(*const CERT_CONTEXT);

    impl Drop for SignerCertificate {
        fn drop(&mut self) {
            // SAFETY: CryptoAPI returned this owned context to the successful verify call.
            unsafe {
                let _ = CertFreeCertificateContext(Some(self.0));
            }
        }
    }

    pub(super) fn verify_detached_manifest(
        manifest_content: &[u8],
        detached_signature: &[u8],
        trust_policy: &ManifestTrustPolicy,
    ) -> Result<(), ManifestVerificationError> {
        if manifest_content.is_empty() {
            return Err(ManifestVerificationError::EmptyContent);
        }
        if detached_signature.is_empty() {
            return Err(ManifestVerificationError::EmptySignature);
        }

        let signature_length = u32::try_from(detached_signature.len())
            .map_err(|_| ManifestVerificationError::InvalidSignature)?;
        let content_length = u32::try_from(manifest_content.len())
            .map_err(|_| ManifestVerificationError::InvalidSignature)?;
        let signer_count = unsafe {
            // SAFETY: Both pointers reference live slices for the duration of the call.
            CryptGetMessageSignerCount(
                (X509_ASN_ENCODING | PKCS_7_ASN_ENCODING).0,
                detached_signature,
            )
        };
        if signer_count != 1 {
            return Err(ManifestVerificationError::InvalidSignature);
        }

        let mut verify_parameters = CRYPT_VERIFY_MESSAGE_PARA::default();
        verify_parameters.cbSize = size_of::<CRYPT_VERIFY_MESSAGE_PARA>() as u32;
        verify_parameters.dwMsgAndCertEncodingType =
            (X509_ASN_ENCODING | PKCS_7_ASN_ENCODING).0;

        let content_pointers = [manifest_content.as_ptr()];
        let content_lengths = [content_length];
        let mut signer_context: *mut CERT_CONTEXT = ptr::null_mut();
        unsafe {
            // SAFETY: The parameter struct, content buffers, signature bytes, and output pointer
            // remain valid for this call. CryptoAPI verifies only the detached signature here.
            CryptVerifyDetachedMessageSignature(
                &verify_parameters,
                0,
                detached_signature,
                1,
                content_pointers.as_ptr(),
                content_lengths.as_ptr(),
                Some(&mut signer_context),
            )
            .map_err(|_| ManifestVerificationError::InvalidSignature)?;
        }
        if signer_context.is_null() {
            return Err(ManifestVerificationError::InvalidSignerCertificate);
        }
        let signer = SignerCertificate(signer_context);

        let context = unsafe {
            // SAFETY: The context is non-null and remains owned by the SignerCertificate guard.
            signer.0.as_ref()
        }
        .ok_or(ManifestVerificationError::InvalidSignerCertificate)?;
        let cert_info = unsafe {
            // SAFETY: A valid CERT_CONTEXT contains the CERT_INFO pointer supplied by CryptoAPI.
            context.pCertInfo.as_ref()
        }
        .ok_or(ManifestVerificationError::InvalidSignerCertificate)?;

        let validity = unsafe {
            // SAFETY: cert_info is live; None requests current system time.
            CertVerifyTimeValidity(None, context.pCertInfo)
        };
        if validity != 0 {
            return Err(ManifestVerificationError::SignerCertificateExpired);
        }

        let subject_length = unsafe {
            // SAFETY: cert_info is live; this call requests the required output length.
            CertNameToStrW(
                X509_ASN_ENCODING,
                &cert_info.Subject as *const _,
                CERT_X500_NAME_STR.0 | CERT_NAME_STR_REVERSE_FLAG,
                None,
            )
        };
        if subject_length < 2 || subject_length > 16_384 {
            return Err(ManifestVerificationError::InvalidSignerCertificate);
        }
        let mut subject_buffer = vec![0_u16; subject_length as usize];
        let written = unsafe {
            // SAFETY: the buffer is allocated to the required length returned above.
            CertNameToStrW(
                X509_ASN_ENCODING,
                &cert_info.Subject,
                CERT_X500_NAME_STR | CERT_NAME_STR_REVERSE_FLAG,
                Some(&mut subject_buffer),
            )
        };
        if written == 0 || written as usize > subject_buffer.len() {
            return Err(ManifestVerificationError::InvalidSignerCertificate);
        }
        let subject_end = subject_buffer
            .iter()
            .position(|character| *character == 0)
            .unwrap_or(subject_buffer.len());
        let subject = String::from_utf16(&subject_buffer[..subject_end])
            .map_err(|_| ManifestVerificationError::InvalidSignerCertificate)?;
        if !subject.eq_ignore_ascii_case(trust_policy.publisher_subject()) {
            return Err(ManifestVerificationError::PublisherSubjectMismatch);
        }

        let encoded_length = context.cbCertEncoded as usize;
        if context.pbCertEncoded.is_null() || encoded_length == 0 {
            return Err(ManifestVerificationError::InvalidSignerCertificate);
        }
        let encoded_certificate = unsafe {
            // SAFETY: CryptoAPI owns a valid certificate buffer of cbCertEncoded bytes.
            slice::from_raw_parts(context.pbCertEncoded, encoded_length)
        };
        let certificate_digest = Sha256::digest(encoded_certificate);
        let actual_pin = encode_lowercase_hex(&certificate_digest);
        if actual_pin != trust_policy.certificate_sha256() {
            return Err(ManifestVerificationError::CertificatePinMismatch);
        }

        Ok(())
    }

    fn encode_lowercase_hex(bytes: &[u8]) -> String {
        const HEX: &[u8; 16] = b"0123456789abcdef";
        let mut output = String::with_capacity(bytes.len() * 2);
        for byte in bytes {
            output.push(HEX[(byte >> 4) as usize] as char);
            output.push(HEX[(byte & 0x0f) as usize] as char);
        }
        output
    }
}

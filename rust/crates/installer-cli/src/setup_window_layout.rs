// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_window_layout.rs
// Amac: Standart kurulum penceresini gorev cubugu disinda ortalamak
// Modul - Rust Windows Adapter
// Version: 0.1.0
// Aciklama: Fiziksel pencere cercevesini monitorun calisma alanina sigdirir
// Bagimli Oldugu Katman: Presentation | Tool

pub const DEFAULT_INNER_SIZE: [f32; 2] = [900.0, 700.0];

#[derive(Debug, PartialEq, Eq)]
struct Placement {
    x: i32,
    y: i32,
    width: i32,
    height: i32,
}

fn centered_placement(work: [i32; 4], outer_size: [i32; 2]) -> Option<Placement> {
    let [left, top, right, bottom] = work;
    let width = right.checked_sub(left)?;
    let height = bottom.checked_sub(top)?;
    if width <= 0 || height <= 0 || outer_size.iter().any(|size| *size <= 0) {
        return None;
    }
    let window_width = outer_size[0].min(width);
    let window_height = outer_size[1].min(height);
    Some(Placement {
        x: left + (width - window_width) / 2,
        y: top + (height - window_height) / 2,
        width: window_width,
        height: window_height,
    })
}

/// @dev-id TI-WINDOW-001
/// @purpose Center the complete setup window inside the monitor work area once.
/// @reason Monitor dimensions alone ignore taskbars, nonzero origins and borders.
/// @contract Use physical Win32 coordinates throughout, preserving DPI scaling;
/// never re-center during navigation or after the user moves/resizes the window.
#[cfg(all(windows, feature = "setup-authoring-gui"))]
pub fn center_on_work_area(
    creation: &eframe::CreationContext<'_>,
) -> Result<(), Box<dyn std::error::Error>> {
    use raw_window_handle::{HasWindowHandle, RawWindowHandle};
    use windows::Win32::{
        Foundation::{HWND, RECT},
        Graphics::Gdi::{
            GetMonitorInfoW, MONITOR_DEFAULTTONEAREST, MONITORINFO, MonitorFromWindow,
        },
        UI::WindowsAndMessaging::{GetWindowRect, SWP_NOACTIVATE, SWP_NOZORDER, SetWindowPos},
    };

    let RawWindowHandle::Win32(handle) = creation.window_handle()?.as_raw() else {
        return Err("TI-GUI-001: Win32 window handle unavailable".into());
    };
    let hwnd = HWND(handle.hwnd.get() as *mut _);
    let mut monitor_info = MONITORINFO {
        cbSize: std::mem::size_of::<MONITORINFO>() as u32,
        ..Default::default()
    };
    let mut outer = RECT::default();
    // SAFETY: eframe owns the live HWND throughout this creation callback.
    // All output pointers refer to initialized, correctly sized stack structs.
    unsafe {
        let monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        GetMonitorInfoW(monitor, &mut monitor_info).ok()?;
        GetWindowRect(hwnd, &mut outer)?;
    }
    let work = monitor_info.rcWork;
    let placement = centered_placement(
        [work.left, work.top, work.right, work.bottom],
        [outer.right - outer.left, outer.bottom - outer.top],
    )
    .ok_or("TI-GUI-001: invalid monitor work area or window dimensions")?;
    // SAFETY: Only this app's live window is moved/resized; z-order and
    // activation are preserved. No borrowed pointer outlives the call.
    unsafe {
        SetWindowPos(
            hwnd,
            None,
            placement.x,
            placement.y,
            placement.width,
            placement.height,
            SWP_NOACTIVATE | SWP_NOZORDER,
        )?;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn centers_outer_frame_above_taskbar() {
        let outer = [
            DEFAULT_INNER_SIZE[0] as i32 + 16,
            DEFAULT_INNER_SIZE[1] as i32 + 39,
        ];
        assert_eq!(
            centered_placement([0, 0, 1920, 1040], outer),
            Some(Placement {
                x: 502,
                y: 150,
                width: 916,
                height: 739
            })
        );
    }

    #[test]
    fn fits_scaled_window_on_small_and_offset_monitors() {
        for work in [
            [0, 0, 1366, 728],
            [-1280, 40, 0, 1024],
            [1920, -900, 3520, 0],
        ] {
            for scale in [1.0, 1.25, 1.5, 2.0] {
                let outer = [
                    ((DEFAULT_INNER_SIZE[0] + 16.0) * scale) as i32,
                    ((DEFAULT_INNER_SIZE[1] + 39.0) * scale) as i32,
                ];
                let p = centered_placement(work, outer).unwrap();
                assert!(p.x >= work[0] && p.y >= work[1]);
                assert!(p.x + p.width <= work[2] && p.y + p.height <= work[3]);
                assert!(((p.x - work[0]) - (work[2] - p.x - p.width)).abs() <= 1);
                assert!(((p.y - work[1]) - (work[3] - p.y - p.height)).abs() <= 1);
            }
        }
    }

    #[test]
    fn rejects_invalid_geometry() {
        assert!(centered_placement([0, 0, 0, 0], [900, 700]).is_none());
        assert!(centered_placement([0, 0, 1920, 1040], [0, 700]).is_none());
        assert!(centered_placement([i32::MIN, 0, i32::MAX, 1040], [900, 700]).is_none());
    }
}

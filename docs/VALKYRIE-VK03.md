# Valkyrie VK03 / VN03M case display (MCVKC03)

Native Linux user-space support for the case display verified with these details:

- USB `345f:9132`, serial-paired HID control and interface 3 bulk OUT `0x04`.
- EDID monitor name **MCVKC03**, preferred timing **360×960**.
- Chip signature `B7 16 0A` at `F000`: **MS912C**, video port **6**.
- Physical panel is landscape **960×360**; newly discovered panels default to
  clockwise 90° rotation, matching the tested landscape-to-controller transform.

Shared USB IDs are insufficient: scanning checks the EDID name and geometry;
opening rechecks them and the protocol refuses any other chip/port before mode
initialization. This panel is kept out of the VMAX and V360 initializers. Legacy
VMAX entries for the same device migrate to Valkyrie with their profile,
brightness, enabled state, rotation and hotkey bindings preserved.

## Setup

Install the bundled `packaging/infopanel-udev.rules` using the normal installer.
Both the USB device node **and** its hidraw node need access; a temporary ACL on a
hidraw node disappears if that node is recreated. Never run two screen-control
applications against the same physical panel at once.

Scan from **Devices**, find **Valkyrie VK03 / VN03M (MCVKC03)** in the Valkyrie
section, create/assign a 960×360 profile and enable its device toggle. The matching
profile button accounts for the selected rotation. Brightness is applied to the
pixel buffer; changing the rotation/brightness invalidates cached frames.

## Protocol and continuous refresh

`Vk03Protocol` implements the measured stable mode: input 360×960 BGR888, output
private **VIC A0**. Every `FF` pixel byte is escaped to `FE`. A frame consists of
an 8-byte header, 1,036,800 pixel bytes and an 8-byte trailer. Bulk writes are
followed by a zero-length packet and an alternating frame-ID trigger. The screen
gate and video output are enabled after the first complete frame. Register
commands poll the MS912C completion registers with finite timeouts.

The panel went black after a single frame even with the process and USB handles
held open. Sending cached static frames at **5 fps** kept it visible on the test
unit. The device defaults to 5 fps and enforces that minimum even when the profile
does not change; resizing/encoding remains cached. Pixel/frame buffers are reused.

Wire-level reference (protocol facts; implementation independently written):
https://github.com/codehz/msdisplay/tree/main/ms2160

## Validation

The test unit reported valid EDID (checksum 0), MCVKC03, 360×960, MS912C/port 6.
The new C# discovery identified its serial-paired VK03 model; the InfoPanel
renderer and USB task streamed the landscape test image at 5 fps; the user confirmed
correct orientation, RGB order and the final full-screen result. Switching between
controller processes occasionally caused one initialization timeout; the existing
supervisor retried and recovered automatically on the next attempt. Automated tests
cover mode commands, initialization guards, frame bytes/escaping, color conversion,
frame-ID alternation, screen-off behavior, minimum cadence and settings migration.
Long-term operation, suspend/resume and hot-unplug recovery need additional hardware
coverage; the task uses the existing device supervisor/retry mechanism.

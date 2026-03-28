# HoyoToon 0.1.0

## HoyoToon Manager
- Added a clear selection button to the models tab.
- Display the current package version on the header of the Manager window.
    - Clicking it will check for updates
    - Status for the following: 
        - Up to date
        - Update available
        - Checking
        - Local ahead
        - Error
- Added Turnaround capture feature to the Renders tab.
    - Capture an orthographic front, back, left, and right view of the model with a single click.
    - Composits the captures into a single image with configurable gap, padding and background image.
    - Will automatically force the watermark on for turnaround captures.
    - Turnaround captures will have the light always facing the view angle, so the lighting will be consistent across all views and unaffected by the model's orientation.
- Reorganized the Renders module UI
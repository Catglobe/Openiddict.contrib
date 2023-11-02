# Release procedure

1. Edit `eng/Versions.props` and update the version number.
2. Set `StabilizePackageVersion` to false if generate a preview-package.
3. Push, wait for CI to complete.
4. Go to release on github, and "Draft a new release".
5. Write same tag as Version number (from `eng/Versions.props`) and press "Generate release notes"
# Mu identity

Original native vector artwork: a friendly muon-inspired particle with a
decorative detector halo and track trail. This is a stylized mascot, not a
scientific depiction of a particle's size, orbit or trajectory. It is also the
Muon avatar preset. `Mu.svg` is scalable, `Mu.png` is a transparent
1024×1024 export, and `Mu.ico` contains 16, 24, 32, 48, 64, 128 and 256 pixel PNG
frames for Windows Explorer, shortcuts, taskbar/window icons and the system tray.

Regenerate all three with the native WPF renderer:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File scripts/render-mu-icon.ps1
```

No generated-image service, font or external artwork is needed. The icon keeps
Mu's Teal identity; avatar palette changes personalize the character without
changing the application/shortcut identity.

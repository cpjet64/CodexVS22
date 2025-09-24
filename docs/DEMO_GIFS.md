Demo GIFs Plan (T10.11)

Scenarios to capture (5–10s each)
- Start chat and send a message; show streaming response.
- Exec console: run command, color output, trim on finish.
- Diff/patch apply: show file tree and result.
- MCP tools: list view and selecting a prompt insertion.

Capture Tools (Windows)
- ScreenToGif (recommended): https://www.screentogif.com/
- ffmpeg (optional cli): `choco install ffmpeg` then use commands below

Capture Tips
- Use 125% UI scale, dark theme.
- Crop to tool window + relevant panels.
- Export as optimized GIF under 4 MB each.

ffmpeg Examples
- Record a region (update coordinates):
  `ffmpeg -f gdigrab -framerate 30 -offset_x 200 -offset_y 150 -video_size 1280x720 -i desktop -t 00:00:10 out.mp4`
- Convert MP4 to GIF (palette for quality):
  `ffmpeg -i out.mp4 -vf "fps=15,scale=1024:-1:flags=lanczos,palettegen" palette.png`
  `ffmpeg -i out.mp4 -i palette.png -lavfi "fps=15,scale=1024:-1:flags=lanczos,paletteuse" chat.gif`

Embed Targets
- README.md (Demo GIFs section)
- VS Marketplace listing description (rich text)

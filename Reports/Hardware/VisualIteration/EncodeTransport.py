"""Encode actual Unity frames with their recorded wall-clock intervals, without interpolating imagery."""
import csv
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(root / "Library/VisualIterationTools"))
import imageio_ffmpeg

folder = Path(__file__).parent / "1920x1080"
frames = folder / "TransportFrames"
if not frames.exists():
    frames = root / "Library/VisualIterationCaptureFrames"
rows = list(csv.DictReader((folder / "TransportMotion.csv").open(encoding="utf-8-sig")))
playlist = root / "Library/VisualIterationTools/transport-concat.txt"
lines = []
for index, row in enumerate(rows):
    path = frames / (str(row["frame"]).zfill(4) + ".png")
    if not path.exists():
        raise FileNotFoundError(path)
    interval = float(rows[index + 1]["realtime"]) - float(row["realtime"]) if index + 1 < len(rows) else 1 / 15
    lines.extend(["file '" + path.as_posix() + "'", "duration " + str(max(.001, interval))])
lines.append(lines[-2])
playlist.write_text("\n".join(lines) + "\n", encoding="utf-8")
subprocess.run([
    imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-hide_banner", "-loglevel", "warning",
    "-f", "concat", "-safe", "0", "-i", str(playlist), "-fps_mode", "vfr",
    "-vf", "scale=1280:-2", "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p",
    "-movflags", "+faststart", str(folder / "Transport.mp4")
], check=True)
print("Encoded", len(rows), "real rendered frames; captured duration", float(rows[-1]["realtime"]) - float(rows[0]["realtime"]), "s")

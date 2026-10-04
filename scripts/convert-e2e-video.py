"""Convert the recorded Playwright workflow to MP4 and GIF using a local FFmpeg."""
import argparse
import re
import subprocess
import sys
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ffmpeg', help='Full path to FFmpeg; otherwise use imageio-ffmpeg from the workspace tool directory.')
    parser.add_argument('--artifact-dir', default='artifacts/e2e', help='Recording artifact directory relative to the project root.')
    args = parser.parse_args()
    project_root = Path(__file__).resolve().parent.parent
    if args.ffmpeg:
        executable = args.ffmpeg
    else:
        sys.path.insert(0, str(project_root / '.superpowers' / 'video-tools'))
        import imageio_ffmpeg
        executable = imageio_ffmpeg.get_ffmpeg_exe()

    artifact_root = project_root / args.artifact_dir
    videos = list((artifact_root / 'playwright-recording').rglob('video.webm'))
    if len(videos) != 1:
        raise SystemExit(f'Expected one workflow recording, found {len(videos)}.')
    source = videos[0]
    mp4 = artifact_root / 'weather-workflow.mp4'
    gif = artifact_root / 'weather-workflow.gif'
    commands = [
        ['-y', '-i', str(source), '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '23',
         '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(mp4)],
        ['-y', '-i', str(source), '-filter_complex',
         'fps=8,scale=960:-1:flags=lanczos,split[s0][s1];[s0]palettegen=stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=5',
         '-loop', '0', str(gif)],
    ]
    for command in commands:
        subprocess.run([executable, '-hide_banner', '-loglevel', 'error', *command], check=True)

    verification = subprocess.run([executable, '-hide_banner', '-i', str(mp4), '-f', 'null', '-'],
                                  capture_output=True, text=True, check=True)
    duration = re.search(r'Duration: ([0-9:.]+)', verification.stderr)
    for artifact in [mp4, gif]:
        if not artifact.is_file() or artifact.stat().st_size == 0:
            raise SystemExit(f'Output is empty: {artifact}')
        print(f'{artifact.name}: {artifact.stat().st_size:,} bytes')
    if duration:
        print(f'MP4 fully decoded; duration {duration.group(1)}')


if __name__ == '__main__':
    main()

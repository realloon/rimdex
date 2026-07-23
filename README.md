# rimdex

Semantic search for RimWorld Workshop mods.

![](https://oss-1259210922.cos.ap-nanjing.myqcloud.com/rimdex-demo.avif)

## Install

### Download

Download the latest rimdex release from [Releases](https://github.com/realloon/rimdex/releases).

- Windows: `rimdex-win-x64.zip`
- macOS (Apple silicon): `rimdex-osx-arm64.zip`

### Add it to your path

- Windows: `mkdir %USERPROFILE%\.local\bin`
- macOS: `mkdir ~/.local/bin`

Skip this step if the directory already exists.

Move the extracted `rimdex` executable into that directory. On macOS, also run:

```sh
chmod +x ~/.local/bin/rimdex
```

Add `%USERPROFILE%\.local\bin` to `PATH` on Windows.

On macOS, add this line to `~/.zshrc`:

```sh
export PATH="$HOME/.local/bin:$PATH"
```

### Check the installation

Run `rimdex --version`. If it prints a version number, installation is complete.

## Configure rimdex

rimdex uses an embedding model. Configure it with:

```sh
rimdex config set \
  --api-key "YOUR_API_KEY" \
  --base-url "https://api.openai.com/v1" \
  --model "text-embedding-3-small"
```

## Build from source

```sh
git clone https://github.com/realloon/rimdex.git
cd rimdex
dotnet publish -c Release
```

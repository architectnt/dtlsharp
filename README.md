# FUR2MP3 Sharp
*rewrite of heemin's* "[*fur2mp3*](https://github.com/HeeminTV/fur2mp3/)"

## requirements
- [AAFC 3](https://github.com/architectnt/aafc) (master audio generation and normalization)
- system installed [Corrscope](https://github.com/corrscope/corrscope)
- system installed Timidity (midi rendering)
- [Furnace](https://github.com/tildearrow/furnace) (duh)
- .NET 8 (runtime & sdk)

## setting up
1. install .NET
2. assign a token in `.core/credential.txt`
3. install the requirements
4. copy furnace executable to `.core/components`
5. put AAFC (so/dll) into `.core` (the filename must be `aafc`)

# compiling
```
dotnet build -c Release -o ./build
```
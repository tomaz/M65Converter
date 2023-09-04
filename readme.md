# Mega 65 Converter

Converts source data into Mega 65 hardware format for easier consumption in your programs.

Usage: 

- `M65Converter --help` for listing all commands
- `M65Converter <command> --help` for help on particular command

# Features

## VIC 4 character layer export

```
M65Converter chars --help
```

- LDtk source:
	- Only works with simple export (option _Extra Files_ / _Super simple export_ in LDtk project settings must be checked)
	- Source is either the path to `data.json` file or the folder where `data.json` file is generated (click _Locate folder_ button at the right of the _Super simple export_ checkbox and find `data.json` file in the sub-folders)
- Aseprite source:
	- Source is the `.aseprite` or `.ase` file itself
	- Single Aseprite frame supported (first one taken if multiple exist)
	- Basic support for layer transparency and blending modes
	- Image and tileset layers supported

`chars` command creates VIC 4 characters layer from given inputs.

### Outputs

- `chars.bin` Character definitions. Supports full or nibble colour mode. All characters are automatically extracted from the source files. This should be copied to address where characters are expected (default is `$10000` but can be changed with `$d068`-`$d06a` registers).

- `chars.pal` Palette. This is automatically detected from characters.

- `colour.bin` Colour data. This should be copied to colour RAM (default is `$ff80000`).

- `layer.bin` The screen data. This should be copied to screen RAM (default is `$0800` but can be changed with `$d060`-`$d063` registers). If you use custom address, you must also set it with `--chars-address` option otherwise generated data won't be correct!

- `layer.inf` Information about generated data. Includes various precalculated sizes and addresses. Use `-v verbose` to get it printed out. See sample code for how it can be used.

### Limitations / requirements

- Only 2 bytes per character are supported for screen and colour data, so make sure you setup your program accordingly. For example:

	```
	lda #%00000111
	sta $d054
	```

- Only 8-bit colour entries are supported, so make sure you setup your program accordingly. For example:

	```
	lda #%00100000
	trb $d031
	```
### Notes

- By default M65Converter will merge all source layers into single one. This keeps the data size minimal and can even support layer transparency and to some extent blending modes (didn't test so might not work well or even at all).

	You can alternatively enable raster-rewrite-buffer mode with `--rrb` option. In this case, each layer will be exported separately with `GOTOX` attribute data in between as expected by Mega 65 hardware. No assembler code change is needed when changing modes (but keep in mind RRB mode generates much larger screen and colour data, so make sure it doesn't overwrite some other parts of RAM when importing).

- Character indices for screen data are generated according to `--chars-address` option which defaults to `$10000` (so first character index is `$400`). If you use different address in your program, make sure to use the same value for M65Converter as well.

- It's possible to provide an image with "base" characters set with `--chars` option. This is useful when you want to keep characters "stable" between different sources (for example different levels) or after source changes. This can be crucial for handling fonts, detecting collisions with particular chars, or implementing animations. In this mode, any additional characters needed to cover the input is still added at the end of base character set.

- At the moment it's not possible to provide source palette, so keep in mind this may change if input data changes. Again, `--chars` option will add some stability here as well.


- `--info 1` (or greater) generates `info.png` informational/debug image. The number represents the pixel scale (1 = 1:1, 2 = 2:1 etc). The image can be useful aid for visual inspection of the generated palette, characters and layer data. But it can be quite slow, especially in RRB mode.

	**IMPORTANT:** if you're running M65Converter from Visual Studio, make sure you copy the ttf font from `Fonts` folder to the location where `M65Converter.exe` is generated. Otherwise texts will not be written and image will lose much of its usefulness. It you're using precompiled binary [from github](https://github.com/tomaz/M65Converter/releases), the font is already included. The included font is [victor mono](https://fonts.google.com/specimen/Victor+Mono).
	
	You can use any other font too (needs to be called `font.ttf` though). You can even have per-project font; the order where `font.ttf` is searched for is:

	1. "current" folder (your project folder - or more specifically: the folder M65Converter is invoked from. For sample program (see below) that's the location of `run.bat` file)
	2. M65Converter.exe folder

- `--verbosity verbose` option prints out a lot of details. Part of the output is similar to the image `--info` option generates. But it also provides insights into how sources are parsed and converted into outputs.

# Mega 65 sample code

Test Kick Assembler code is attached to this repository. You can find it in `KickAssembler` folder. Launch `run.bat` file to run M65Converter, assemble and launch Xemu emulator. You will likely need to modify the batch file to point to locations where converter, kick assembler and emulator is installed.

Requirements:
- M65Converter: batch file assumes debug version from Visual Studio is present.
- Kick Assembler: you need the version with 65CE02 support. [Download from here](https://gitlab.com/jespergravgaard/kickassembler65ce02).
- Xemu emulator: [Download from here](https://65site.de/emulator.php)
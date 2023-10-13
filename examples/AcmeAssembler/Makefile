SRC_FILE := main.acme
SRC_DIR := src

ASSET_FILE := level.ldtk
ASSET_DIR := assets
ASSET_LDTK_DIR := ldtk
ASSET_ASEPRITE_DIR := aseprite

DST_FILE := game
DST_DIR := build
DST_ASSETS := $(DST_DIR)/$(ASSET_DIR)

#------------------------------------------------------------

POWERSHELL := pwsh.exe -NoProfile -Command
PROGRAMS := ..\..\..\..
ACME := $(PROGRAMS)\development\assembler\Acme\acme.exe
XEMU := $(PROGRAMS)\emulator\Xemu\xmega65.exe
M65CONV := $(PROGRAMS)\tools\M65Converter\src\M65Converter\bin\Debug\net7.0\M65Converter.exe

#------------------------------------------------------------

IN_LEVEL_DIR := $(ASSET_DIR)/$(ASSET_LDTK_DIR)
IN_SPRITE_DIR := $(ASSET_DIR)/$(ASSET_ASEPRITE_DIR)

LdtkSrcPath = $(IN_LEVEL_DIR)/$(subst .ldtk,,$(1))/simplified/AutoLayer/data.json

OUT_LEVEL := $(DST_ASSETS)/chars.bin
OUT_PRG := $(DST_DIR)/$(DST_FILE)

#------------------------------------------------------------

.PHONY: run build prepare clean

# builds and runs the program in emulator
run: build
	$(XEMU) -besure -uartmon :4510 -prg $(OUT_PRG).prg

# builds the program in build folder
build: $(OUT_LEVEL) $(OUT_PRG).prg

# deletes the build folder
clean:
	$(POWERSHELL) Remove-Item -Force -Recurse -Path $(DST_DIR)	

#------------------------------------------------------------

# builds the main .prg
$(OUT_PRG).prg: $(DST_DIR)/*.acme
	$(ACME) \
		--color \
		--cpu m65 \
		-f cbm \
		-I $(DST_DIR) \
		-o $(OUT_PRG).prg \
		-r $(OUT_PRG).rep \
		-l $(OUT_PRG).sym \
		$(DST_DIR)/$(SRC_FILE)

# builds level and sprite binary data; this is purposely bound to chars.bin output so that level is only generated if that file is older than the given source files
$(OUT_LEVEL): $(IN_LEVEL_DIR)/*.ldtk $(IN_SPRITE_DIR)/*.aseprite
	$(M65CONV) \
		--verbosity debug \
		--colour ncm \
		--info 2 \
		--screen-address 0x10000 \
		--chars-address 0x20000 \
		chars \
		--out-chars $(DST_ASSETS)/chars.bin \
		--out-palette $(DST_ASSETS)/chars.pal \
		screens \
		--out-screen $(DST_ASSETS)/level/screen.bin \
		--out-colour $(DST_ASSETS)/level/colour.bin \
		--out-lookup $(DST_ASSETS)/level/lookup.inf \
		--out-info $(DST_ASSETS)/level/info.png \
		$(call LdtkSrcPath,$(ASSET_FILE)) \
		rrbsprites \
		--append-screens \
		--position 87,-15 130,2 10,30 \
		--out-frames $(DST_ASSETS)/spr-{name}.bin \
		--out-lookup $(DST_ASSETS)/spr-{name}.inf \
		$(IN_SPRITE_DIR)/player-idle.aseprite \
		$(IN_SPRITE_DIR)/player-run.aseprite

# creates build folder and copies all source files to it
# we build sources from build folder to allow m65dbg to properly use source code dissasembler - it doesn't work if sources are in other folder
$(DST_DIR)/*.acme: $(SRC_DIR)/$(SRC_FILE)
	$(POWERSHELL) New-Item -Force -ItemType Directory -Path $(DST_DIR) -ErrorAction SilentlyContinue
	$(POWERSHELL) Copy-Item -Path $(SRC_DIR)/*.acme $(DST_DIR)


.cpu _45gs02

.label CHARS_RAM	= $0020000
.label SCREEN_RAM	= $0010000
.label COLOUR_RAM	= $ff80000

BasicUpstart65(Entry)

* = $2400 "Program"
Entry:
{
		sei

		jsr M65.DisableC64ROM
		jsr M65.DisableC65ROM
		jsr M65.Enable40Mhz
		jsr M65.EnableVIC4Registers
		jsr M65.DisableVIC2HotRegisters
		jsr M65.Enable40ColumnMode
		jsr M65.Enable8BitColour
		jsr M65.Enable16BitChars
		jsr M65.EnableRAMPalettes
		jsr M65.DisableCIAInterrupts
		jsr M65.DisableRasterInterrupts

		cli

		jsr InitScreen

		jmp *
}

InitScreen:
{
		VIC4_SetCharLocation(CHARS_RAM)
		VIC4_SetScreenLocation(SCREEN_RAM)

		jsr Screen.SetupRows
		jsr Screen.CopyPalette
		jsr Screen.CopyCharacters
		jsr Screen.CopyColours
		jsr Screen.CopyLayer
}

.namespace Screen
{
	SetupRows:
	{
			// setup logical row size
			lda Data.Info.RowLogicalWidth
			sta $d058
			lda Data.Info.RowLogicalWidth + 1
			sta $d059

			// setup number of chars per row
			lda Data.Info.LayerWidth
			sta $d05e

			// setup number of rows
			lda Data.Info.ScreenHeight
			sta $d07b

			rts
	}

	CopyPalette:
	{
		.label LENGTH = [Data.__Palette - Data.Palette] / 3

			// edit palette 0, chars palette = 0, sprites = 1, alt = 2
			lda #%00000110
			sta $d070

			ldx #0
		!:
			lda Data.Palette, x					// red
			sta $d100, x
			lda Data.Palette + LENGTH, x		// green
			sta $d200, x
			lda Data.Palette + LENGTH * 2, x	// blue
			sta $d300, x

			inx
			cpx #LENGTH
			bne !-

			rts
	}

	CopyCharacters:
	{
		.label SOURCE = Data.Characters
		.label LENGTH = Data.__Characters - SOURCE
		
			DMA_Execute Job
			rts

		Job:
			DMA_JobNew SOURCE : CHARS_RAM
			DMA_JobCopy #LENGTH
	}

	CopyLayer:
	{
		.label SOURCE = Data.Layer
		.label LENGTH = Data.__Layer - SOURCE

			DMA_Execute Job
			rts

		Job:
			DMA_JobNew SOURCE : SCREEN_RAM
			DMA_JobCopy #LENGTH
	}

	CopyColours:
	{
		.label SOURCE = Data.Colours
		.label LENGTH = Data.__Colours - SOURCE

			DMA_Execute Job
			rts

		Job:
			DMA_JobNew SOURCE : COLOUR_RAM
			DMA_JobCopy #LENGTH
	}

	FillScreenRam:
	{
		.label VALUE = $0000

			DMA_Execute Job
			rts

		Job:
			DMA_JobNew #VALUE : SCREEN_RAM
			DMA_JobFill Data.Info.ScreenSize
	}

	FillColourRam:
	{
		.label VALUE = $0000

			DMA_Execute Job
			rts

		Job:
			DMA_JobNew #VALUE : COLOUR_RAM
			DMA_JobFill Data.Info.ScreenSize
	}
}

#import "m65macros.s"

.namespace Data {

	// When all is commented LDtk level export is taken.
	//#define LEVEL1
	//#define LEVEL2

	Palette:
		* = * "Data - Palette"
		.import binary "../build/assets/chars.pal"
	__Palette:

	Characters:
		* = * "Data - Chars"
		.import binary "../build/assets/chars.bin"
	__Characters:

	Layer:
		* = * "Data - Layer"
		#if LEVEL2
			.import binary "../build/assets/level2/screen.bin"
		#elif LEVEL1
			.import binary "../build/assets/level1/screen.bin"
		#else
			.import binary "../build/assets/level/screen.bin"
		#endif
	__Layer:

	Colours:
		* = * "Data - Colour"
		#if LEVEL2
			.import binary "../build/assets/level2/colour.bin"
		#elif LEVEL1
			.import binary "../build/assets/level1/colour.bin"
		#else
			.import binary "../build/assets/level/colour.bin"
		#endif
	__Colours:

	Info:
	{
		* = * "Data - Info"

		.label LayerWidth = * + 20
		.label RowLogicalWidth = * + 24
		.label ScreenHeight = * + 6
		.label ScreenSize = * + 16
		.label Lookup1 = * + 34
		.label Lookup2 = * + 59
		.label Lookup3 = * + 84

		#if LEVEL2
			.import binary "../build/assets/level2/lookup.inf"
		#elif LEVEL1
			.import binary "../build/assets/level1/lookup.inf"
		#else
			.import binary "../build/assets/level/lookup.inf"
		#endif
	}
}

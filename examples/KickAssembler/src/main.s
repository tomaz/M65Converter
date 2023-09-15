.cpu _45gs02

.label CHARS_RAM	= $0020000
.label SCREEN_RAM	= $0010000
.label COLOUR_RAM	= $ff80000

* = $0002 "Zero Page" virtual
.namespace ZP
{
	ScreenLookupPtrLo: .word 0
	ScreenLookupPtrMid: .word 0
	ScreenLookupPtrHi: .word 0
	ScreenLookupRow: .dword 0

	FrameLookupPtrLo: .word 0
	FrameLookupPtrHigh: .word 0
	FrameLookup: .word 0

	Temp1: .byte 0
	Temp2: .byte 0
}

BasicUpstart65(Entry)

THIS CODE IS NOT WORKING (APART FROM SHOWING BACKGROUND) - I ABANDONED IT FOR THE MOMENT BEING IN FAVOR OF ACME ASSEMBLER SOLUTION, UNTIL m65dbg BETTER SUPPORTS KICK...

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

		jsr Screen.Init
		jsr Sprite.Init
		jsr MainLoop
}

MainLoop:
{
		jsr WaitRaster

		jsr Sprite.Move
		jsr Sprite.Apply

		jmp MainLoop
}

WaitRaster:
{
		// wait line
	!:	lda #$fe
		cmp $d012
		bne !-

		// wait until next line
	!:	lda #$ff
		cmp $d012
		bne !-
}

.function NextArgByte(arg)
{
	.if (arg.getType() == AT_IMMEDIATE)
	{
		.return CmdArgument(arg.getType(), >arg.getValue())
	}
	.return CmdArgument(arg.getType(), arg.getValue() + 1)
}

.pseudocommand SpriteSetRRBLookup lo : mid : high
{
		lda lo
		sta ZP.ScreenLookupPtrLo
		lda NextArgByte(lo)
		sta ZP.ScreenLookupPtrLo + 1

		lda mid
		sta ZP.ScreenLookupPtrMid
		lda NextArgByte(mid)
		sta ZP.ScreenLookupPtrMid + 1

		lda high
		sta ZP.ScreenLookupPtrHi
		lda NextArgByte(high)
		sta ZP.ScreenLookupPtrHi + 1
}

.pseudocommand SpriteSetFrameLookup lo : high
{
		lda lo
		sta ZP.FrameLookupPtrLo
		lda NextArgByte(lo)
		sta ZP.FrameLookupPtrLo + 1

		lda high
		sta ZP.FrameLookupPtrHigh
		lda NextArgByte(high)
		sta ZP.FrameLookupPtrHigh + 1
}

.namespace Screen
{
	Init:
	{
			VIC4_SetCharLocation(CHARS_RAM)
			VIC4_SetScreenLocation(SCREEN_RAM)

			jsr Screen.SetupRows
			jsr Screen.CopyPalette
			jsr Screen.CopyCharacters
			jsr Screen.CopyColours
			jsr Screen.CopyScreen
	}

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

	CopyScreen:
	{
		.label SOURCE = Data.Screen
		.label LENGTH = Data.__Screen - SOURCE

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

.namespace Sprite
{
	.label VELOCITY_X = 128
	.label VELOCITY_Y = 64
	
	Init:
	{
			// we'll only move sprite 0
			ldy #0			// Y <- sprite index

			// setup initial X position
			lda #0			// A <- X high byte
			ldx #87			// X <- X low byte
			jsr Direct.Position.SetX

			// setup initial Y position
			lda #0			// A <- Y position
			jsr Direct.Position.SetY

			// setup initial X velocity
			lda #>VELOCITY_X
			ldx #<VELOCITY_X
			jsr Direct.Velocity.SetX

			// setup initial Y velocity
			lda #>VELOCITY_Y
			ldx #<VELOCITY_Y
			jsr Direct.Velocity.SetY

			rts
	}

	Move:
	{
			// we only move sprite 0, apply current velocity
			ldy #0
			jsr Direct.Velocity.Apply

		CheckRight:
			// if X position goes above 200, reverse
			lda Data.XLo
			cmp #200
			bmi CheckLeft

			lda #>(-VELOCITY_X)
			ldx #<(-VELOCITY_X)
			jsr Direct.Velocity.SetX
			bra CheckBottom

		CheckLeft:
			// if X position goes below 50, reverse
			lda Data.XLo
			cmp #50
			bpl CheckBottom

			lda #>VELOCITY_X
			ldx #<VELOCITY_X
			jsr Direct.Velocity.SetX

		CheckBottom:
			// if Y position goes over 150, reverse
			lda Data.YLo
			cmp #150
			bmi CheckTop

			lda #>(-VELOCITY_Y)
			ldx #<(-VELOCITY_Y)
			jsr Direct.Velocity.SetY
			bra End

		CheckTop:
			// if Y position goes below #10, reverse
			lda Data.YLo
			cmp #10
			bpl End

			lda #>VELOCITY_Y
			ldx #<VELOCITY_Y
			jsr Direct.Velocity.SetY

		End:
			rts
	}

	Apply:
	{
		// setup ZP GOTOX lookup table ptr. We expect data to be located in the bottom 64K to keep things simple for now, but the data itself can point to higher memory.
		// lda #<@Data.Info.SpriteIdleLookupLo
		// sta ZP.ScreenLookupPtrLo
		// lda #>@Data.Info.SpriteIdleLookupLo
		// sta ZP.ScreenLookupPtrLo + 1

		// lda #<@Data.Info.SpriteIdleLookupMid
		// sta ZP.ScreenLookupPtrMid
		// lda #>@Data.Info.SpriteIdleLookupMid
		// sta ZP.ScreenLookupPtrMid + 1

		// lda #<@Data.Info.SpriteIdleLookupHigh
		// sta ZP.ScreenLookupPtrHi
		// lda #>@Data.Info.SpriteIdleLookupHigh
		// sta ZP.ScreenLookupPtrHi + 1

		// setup ZP frames lookup ptr. We expect data to be located in the bottom 64K to keep things simple for now. We expect the data itself to point to the bottom 64K also.
		// lda #<Sprite.Data.IdleFrameLookupLo
		// sta ZP.FrameLookup
		// lda #>Sprite.Data.IdleFrameLookupHigh
		// sta ZP.FrameLookup + 1

		ldy #0					// sprite 0

		// set frame
		lda #0
		jsr Direct.Frame.Set

		// apply position & frame to RRB screen data
		SpriteSetRRBLookup #@Data.Info.SpriteIdleLookupLo : #@Data.Info.SpriteIdleLookupMid : #@Data.Info.SpriteIdleLookupHigh
		SpriteSetFrameLookup #Sprite.Data.IdleFrameLookupLo : #Sprite.Data.IdleFrameLookupHigh
		jsr Direct.Frame.ApplyRRB
	}

	.namespace Direct
	{
		.namespace Frame
		{
			// A = frame index
			// Y = sprite index
			Set:
			{
					sta Data.Frame, y
					rts
			}

			// Y = sprite index
			// `ZP.ScreenLookupPtrLo` points to GOTOX lookup table for sprite for low byte
			// `ZP.ScreenLookupPtrMid` points to GOTOX lookup table for sprite for mid byte
			// `ZP.ScreenLookupPtrHigh` points to GOTOX lookup table for sprite for high byte
			// `ZP.FrameLookupPtrLo` points to frames lookup table for low byte
			// `ZP.FrameLookupPtrHigh` points to frames lookup table for high byte
			// NOTE: this routine is not very efficient, but does get the job done ¯\_(ツ)_/¯
			ApplyRRB:
			{
				.label SPR_TOP_ROW = ZP.Temp1
				.label SPR_BOTTOM_ROW = ZP.Temp2

					// apply Y offset
					lda Data.YLo, y			// A <- y position on screen
					and #$07				// A <- only bottom 3 bits
					eor #$07				// A <- bottom 3 bits reversed = %00000yyy (we need these 3 bits moved from 2-0 to 7-5)
					asl						// A <- %0000yyy0
					asl						// A <- %000yyy00
					asl						// A <- %00yyy000
					asl						// A <- %0yyy0000
					asl						// A <- %yyy00000
					sta YOffset				// store to the loop below (self mod code)

					// we will use `ZP.ScreenLookupRow` DWORD pointer for accessing row addresses. We only have 3 bytes of data there though, top byte is always 0
					lda #0
					sta ZP.ScreenLookupRow + 3

					// prepare pointer to current frame data in ZP
					lda Data.Frame, y				// A <- frame index
					taz								// Z <- frame index
					lda (ZP.FrameLookupPtrLo), z	// A <- low byte of frame data address
					sta ZP.FrameLookup
					lda (ZP.FrameLookupPtrHigh), z	// A <- high byte of frame data address
					sta ZP.FrameLookup + 1

					// determine top and bottom screen rows where we will draw the sprite.
					lda Data.YLo, y			// A <- y position on screen (%yyyyyyyy)
					lsr						// A <- %0yyyyyyy (A = A / 2)
					lsr						// A <- %00yyyyyy (A = A / 4)
					lsr						// A <- %000yyyyy (A = A / 8)
					sta SPR_TOP_ROW
					clc
					adc #6					// hard coded for now, sprite height + 2 transparent chars rows
					sta SPR_BOTTOM_ROW

					// we need to apply the Y offset to each and every row
					ldz #0					// Z <- row counter
				!nextRow:
					// load the actual row offset into ZP memory. We support 4 bytes, but MSB is always 0 since VIC4 only has access to bottom 384K
					lda (ZP.ScreenLookupPtrLo), z	// A <- low byte for screen row address
					sta ZP.ScreenLookupRow
					lda (ZP.ScreenLookupPtrMid), z	// A <- mid byte for screen row address
					sta ZP.ScreenLookupRow + 1
					lda (ZP.ScreenLookupPtrHi), z	// A <- high byte for screen row address
					sta ZP.ScreenLookupRow + 3

					// prepare screen RAM byte 0:
					//  -------+ X low byte
					// %xxxxxxxx
					lda Data.XLo, y			// A <- X low byte
					ldx #0					// first byte of screen GOTOX ram
					sta ((ZP.ScreenLookupRow)), x

					// prepare screen RAM byte 1:
					//    Y offset
					//  --+   -+ X upper 2 bits
					// %YYY000XX
					lda Data.XHi, y			// A <- X high byte
					and %00000011			// A <- clear all bits but 1-0 (%000000xx)
					ora YOffset: $ff		// A <- A or Y offset (%yyy000xx)
					inx						// second byte of screen GOTOX ram
					sta ((ZP.ScreenLookupRow)), x

					!spriteColumnLoop:
						// we will repurpose Y reg for sprite frame indexes, so we need to restore it afterwards
						inx						// screen RAM indexer = first sprite data byte
						phy
						ldy #0

					!nextSpriteColumn:
						// this part uses hard-coded sprite witdh of 2 chars (4 bytes), so plus 2 bytes for GOTOX = X from 2..5
						// we could improve by reading this data from sprite lookup tables, but this is good enough for this demonstration
						lda (ZP.FrameLookup), y			// A <- next byte
						sta (ZP.ScreenLookupRow), x		// save it to screen RAM data

						inx						// next sprite data byte
						cpx #6					// did we reach end of sprite data?
						bne !nextSpriteColumn-	// nope, continue with next byte

					!updateSpriteDataPtr:
						// if we're above row in which we should start drawing sprite, we should continue rendering top transparent chars from frame data.
						tza						// A <- current screen row
						cmp SPR_TOP_ROW			// A == top sprite row?
						bmi !completeSpriteLoop+// still above, nothing to change

						// if we're below bottom row, we should continue rendering bottom transparent chars from frame data
						cmp SPR_BOTTOM_ROW		// A == bottom sprite row?
						beq !completeSpriteLoop+// just completed bottom row, no need to change anything
						bpl !completeSpriteLoop+// below bottom row, no need to changte anythingb

						// update frame pointer to next row
						clc
						lda ZP.FrameLookup		// A <- frame ptr low byte
						adc #4					// A <- A + 4 - each row has 4 bytes (again hard-coded to simplify this example)
						sta ZP.FrameLookup
						bcc !+					// if no carry skip high byte
						inc ZP.FrameLookup + 1	// if carry set, increment high byte
					!:

					!completeSpriteLoop:
						// restore registers
						ply						// Y <- sprite index

				!prepareForNextRow:
					// proceed with next row if we still have some
					inz						// Z <- Z + 1
					cpz #25					// did we reach end of screen rows?
					bne !nextRow-			// no, repeat with next row

					rts
			}
		}

		.namespace Velocity
		{
			// A = low byte
			// X = fractional
			// Y = sprite index
			SetX:
			{
					sta Data.XVLo, y
					stx Data.XVFrac, y
					rts
			}

			// A = low byte
			// X = fractional
			// Y = sprite index
			SetY:
			{
					sta Data.YVLo, y
					stx Data.YVFrac, y
					rts
			}

			// Y = sprite index
			// 
			Apply:
			{
				X:
					ldx Data.XVLo, y
					lda Data.XVFrac, y
					bmi !sub+
					jsr Direct.Position.AddX
					bra Y
				!sub:
					jsr Direct.Position.SubX

				Y:
					ldx Data.YVLo, y
					lda Data.YVFrac, y
					bmi !sub+
					jsr Direct.Position.AddY
					rts
				!sub:
					jsr Direct.Position.SubY

			}
		}

		.namespace Position
		{
			// A = high byte
			// X = low byte
			// Y = sprite index
			// fraction reset to 0
			SetX:
			{
					sta Data.XHi, y
					stx Data.XLo, y
					
					lda #0
					sta Data.XFrac, y

					rts
			}

			// A = fractional
			// X = low byte
			// Y = sprite index
			AddX:
			{
					clc
					adc Data.XFrac, y		// A <- A + current fractional
					sta Data.XFrac, y		// store fractional to data

					txa						// A <- X = low byte
					adc Data.XLo, y			// A <- A + current low byte + carry
					sta Data.XLo, y			// store low byte to data

					lda Data.XHi, y			// A <- current high byte
					adc #0					// A <- A + 0 + carry
					sta Data.XHi, y			// store high byte to data

					rts
			}

			// A = fractional
			// X = low byte
			// Y = sprite index
			SubX:
			{
					// this routine uses self modifying code since we need to do "current - reg"

					sec
					sta Fractional			// A -> subtrahend (self mod)
					lda Data.XFrac, y		// A = current fractional
					sbc Fractional: $ff		// A <- A - new fractional
					sta Data.XFrac, y		// store subtraction result

					txa						// A <- X = low byte
					sta Low					// A -> subtrahend (self mod)
					lda Data.XLo, y			// A = current low value
					sbc Low: $ff			// A <- A - new low value - carry
					sta Data.XLo, y			// store low result

					lda Data.XHi, y			// A <- high byte
					sbc #0					// A <- A - 0 - carry
					sta Data.XHi, y			// store high result

					rts
			}

			// A = low byte
			// Y = sprite index
			// fraction reset to 0
			SetY:
			{
					sta Data.YLo, y

					lda #0
					sta Data.YFrac, y

					rts
			}

			// A = fractional
			// X = low byte
			// Y = sprite index
			AddY:
			{
					clc
					adc Data.YFrac, y		// A <- A + current fractional
					sta Data.YFrac, y		// store fractional to data

					txa						// A <- X = low byte
					adc Data.YLo, y			// A <- A + current low byte + carry
					sta Data.YLo, y			// store low byte to data

					rts
			}

			// A = fractional
			// X = low byte
			// Y = sprite index
			SubY:
			{
					// this routine uses self modifying code since we need to do "current - reg"

					sec
					sta Fractional			// A -> subtrahend (self mod)
					lda Data.YFrac, y		// A = current fractional
					sbc Fractional: $ff		// A <- A - new fractional
					sta Data.YFrac, y		// store subtraction result

					txa						// A <- X = low byte
					sta Low					// A -> subtrahend (self mod)
					lda Data.YLo, y			// A = current low value
					sbc Low: $ff			// A <- A - new low value - carry
					sta Data.YLo, y			// store low result

					rts
			}
		}
	}

	.namespace  Data
	{
		.label COUNT = 2

		Frame: .fill COUNT, 0

		XVLo: .fill COUNT, 0
		XVFrac: .fill COUNT, 0

		YVLo: .fill COUNT, 0
		YVFrac: .fill COUNT, 0

		XHi: .fill COUNT, 0
		XLo: .fill COUNT, 0
		XFrac: .fill COUNT, 0

		YLo: .fill COUNT, 0
		YFrac: .fill COUNT, 0

		// the data is hard coded for now, ideally we should generate this from sprite lookup tables...
		IdleFrameLookupLo:
			.fill 11, <[@Data.SpriteIdle + i * 16]
		IdleFrameLookupHigh:
			.fill 11, >[@Data.SpriteIdle + 1 * 16]
	}
}

#import "m65macros.s"

.namespace Data {

	Palette:
		* = * "Data - Palette"
		.import binary "../build/assets/chars.pal"
	__Palette:

	Characters:
		* = * "Data - Chars"
		.import binary "../build/assets/chars.bin"
	__Characters:

	Screen:
		* = * "Data - Screen"
		.import binary "../build/assets/level/screen.bin"
	__Screen:

	Colours:
		* = * "Data - Colour"
		.import binary "../build/assets/level/colour.bin"
	__Colours:

	SpriteIdleLookup:
		.import binary "../build/assets/spr-player-idle.inf"
	SpriteIdle:
		* = * "Data - Sprite - Idle"
		.import binary "../build/assets/spr-player-idle.bin"
	__SpriteIdle:

	SpriteRunLookup:
		.import binary "../build/assets/spr-player-run.inf"
	SpriteRun:
		.import binary "../build/assets/spr-player-run.bin"
	__SpriteRun:

	Info:
	{
		* = * "Data - Info"

		// Offsets were copied from M65Converter cmd line output
		.label LayerWidth = * + 18
		.label RowLogicalWidth = * + 22
		.label ScreenHeight = * + 6
		.label ScreenSize = * + 10

		.label SpriteIdleLookupLo = * + 110
		.label SpriteIdleLookupMid = * + 136
		.label SpriteIdleLookupHigh = * + 162

		.label SpriteRunLookupLo = * + 188
		.label SpriteRunLookupMid = * + 214
		.label SpriteRunLookupHigh = * + 240

		.import binary "../build/assets/level/lookup.inf"
	}
}

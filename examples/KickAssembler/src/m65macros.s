.cpu _45gs02

///////////////////////////////////////////////////////////////////////////////
// ROUTINES
///////////////////////////////////////////////////////////////////////////////

.namespace M65
{

	Enable40Mhz:
	{
			lda #$41
			sta $00
			rts
	}

	EnableVIC3Registers:
	{
			lda #$00
			tax 
			tay 
			taz 
			map
			eom

			lda #$A5	//Enable VIC III
			sta $d02f
			lda #$96
			sta $d02f

			rts
	}

	EnableVIC4Registers:
	{
			lda #$00
			tax
			tay
			taz
			map
			eom

			lda #$47	// Enable VIC IV
			sta $d02f
			lda #$53
			sta $d02f

			rts
	}

	Enable40ColumnMode:
	{
			lda #%10000000
			trb $d031

			rts
	}

	Enable8BitColour:
	{
			// Disable VIC3 ATTR register to enable 8bit color
			lda #%00100000
			trb $d031

			rts
	}

	EnableRAMPalettes:
	{
			// Enable RAM palettes
			lda #$04
			tsb $d030

			rts
	}

	Enable16BitChars:
	{
			// Turn on FCM mode and
			// 16bit per char number
			// bit 0 = Enable 16 bit char numbers
			// bit 1 = Enable FCM for chars <= $ff
			// bit 2 = Enable FCM for chars > $ff
			lda #%00000111
			sta $d054

			rts
	}

	DisableVIC2HotRegisters:
	{
			// disable VIC2 multi colour mode
			lda #%00010000
			trb $d016
			
			// Disable hot registers by turning off bit 7 
			lda #%10000000
			trb $d05d		// wont destroy VIC4 values (bit 7)

			rts
	}

	DisableC64ROM:
	{
			lda #$35
			sta $01

			rts
	}

	DisableC65ROM:
	{
			lda #$70
			sta $d640
			eom

			// Unmap C65 Roms $d030 by clearing bits 3-7
			lda #%11111000
			trb $d030

			rts
	}

	DisableCIAInterrupts:
	{
			lda #$7f
			sta $dc0d
			sta $dd0d

			rts
	}

	DisableRasterInterrupts:
	{
			// Disable IRQ raster interrupts because C65 uses raster interrupts in the ROM
			lda #$00
			sta $d01a

			rts
	}
}

///////////////////////////////////////////////////////////////////////////////
// GENERAL MACROS
///////////////////////////////////////////////////////////////////////////////

.macro BasicUpstart65(addr)
{
	* = $2001 "Basic"

		.var addrStr = toIntString(addr)

		.byte $09,$20 //End of command marker (first byte after the 00 terminator)
		.byte $0a,$00 //10
		.byte $fe,$02,$30,$00 //BANK 0
		.byte <end, >end //End of command marker (first byte after the 00 terminator)
		.byte $14,$00 //20
		.byte $9e //SYS
		.text addrStr
		.byte $00
	end:
		.byte $00,$00	//End of basic terminators
}

.macro MapMemory(source, target)
{
	.var sourceMB = (source & $ff00000) >> 20
	.var sourceOffset = ((source & $00fff00) - target)
	.var sourceOffHi = sourceOffset >> 16
	.var sourceOffLo = (sourceOffset & $0ff00 ) >> 8
	.var bitLo = pow(2, (((target) & $ff00) >> 12) / 2) << 4
	.var bitHi = pow(2, (((target-$8000) & $ff00) >> 12) / 2) << 4
	
	.if (target < $8000) {
		lda #sourceMB
		ldx #$0f
		ldy #$00
		ldz #$00
	} else {
		lda #$00
		ldx #$00
		ldy #sourceMB
		ldz #$0f
	}
	map 

	//Set offset map
	.if (target < $8000)
	{
		lda #sourceOffLo
		ldx #[sourceOffHi + bitLo]
		ldy #$00
		ldz #$00
	} else {
		lda #$00
		ldx #$00
		ldy #sourceOffLo
		ldz #[sourceOffHi + bitHi]
	}	
	map 
	eom
}

.macro VIC4_SetCharLocation(addr)
{
	lda #[addr & $ff]
	sta $d068
	lda #[[addr & $ff00]>>8]
	sta $d069
	lda #[[addr & $ff0000]>>16]
	sta $d06a
}

.macro VIC4_SetScreenLocation(addr)
{
	lda #[addr & $ff]
	sta $d060
	lda #[[addr & $ff00]>>8]
	sta $d061
	lda #[[addr & $ff0000]>>16]
	sta $d062
	lda #[[[addr & $ff0000]>>24] & $0f]
	sta $d063
}

///////////////////////////////////////////////////////////////////////////////
// DMA
///////////////////////////////////////////////////////////////////////////////

.label CHAINED = 1
.label FORWARD = 0
.label BACKWARDS = 1

.var DMASource = 0
.var DMADestination = 0

//-----------------------------------------------------------------------------
// Runs the given JOB
.pseudocommand DMA_Execute address
{
	.var addr = address.getValue()

		lda #[addr >> 16]
		sta $d702
		sta $d704
		lda #>addr
		sta $d701
		lda #<addr
		sta $d705
}

//-----------------------------------------------------------------------------
.pseudocommand DMA_JobNew source : destination {
	.eval DMASource = source
	.eval DMADestination = destination

		// request format is F018A
		.byte $0A

		// source bank
		.byte $80
		.if (source.getType() == AT_NONE) {
			.byte $00
		} else {
			.byte [source.getValue() >> 20]
		}

		// destination bank.
		.byte $81
		.if (destination.getType() == AT_NONE) {
			.byte $00
		} else {
			.byte [destination.getValue() >> 20]
		}
}


//-----------------------------------------------------------------------------
// Writes the header for a new job using the given source and destination
//
// This also remembers source and destination and will automatically be used when specifying specific DMA commands.
//
// Parameters:
// - source source address or value
// - destination destination address
.pseudocommand DMA_JobHeader source : destination
{
		// request format is F018A
		.byte $0A

		// source bank
		.byte $80
		.if (source.getType() == AT_NONE) {
			.byte $00
		} else {
			.byte [source.getValue() >> 20]
		}

		// destination bank.
		.byte $81
		.if (destination.getType() == AT_NONE) {
			.byte $00
		} else {
			.byte [destination.getValue() >> 20]
		}
}

//-----------------------------------------------------------------------------
// Writes a copy DMA job.
//
// Requires prior call to `DMA_JobHeader` to define source and destination.
//
// Parameters:
// - length Length of the copy in bytes
// - backwards (optional) If present, backwards copy is performed. If omitted, forward job is assumed.
// - chained (optional) If present, another job will follow. If ommited, this is the last job.
.pseudocommand DMA_JobCopy length : backwards : chained {
	.var SizeVal = length.getValue()
	.var SourceVal = DMASource.getValue()
	.var DestinationVal = DMADestination.getValue()

	.var IsChained = false
	.if (chained.getType() != AT_NONE) {
		.eval IsChained = true
	}

	.var BackVal = $00
	.if (backwards.getType() != AT_NONE) {
		.eval BackVal = $40
		.eval SourceVal = SourceVal + SizeVal - 1
		.eval DestinationVal = SourceVal + SizeVal - 1
	}

	.var MarkerVal = $00
	.if (IsChained) {
		.eval MarkerVal = $04
	}

		.byte $00			// no more options
		.byte MarkerVal
		.word SizeVal
		.word [SourceVal & $ffff]
		.byte [SourceVal >> 16] + BackVal
		.word [DestinationVal & $ffff]
		.byte [[DestinationVal>> 16] & $0f] + BackVal

		.if (IsChained) {
			.word $0000
		}
}

//-----------------------------------------------------------------------------
// Writes a fill DMA job.
//
// Requires prior call to `DMA_JobHeader` to define source and destination.
//
// Parameters:
// - length Length of the copy in bytes
// - chained (optional) If present, another job will follow. If ommited, this is the last job.
.pseudocommand DMA_JobFill length : chained
{
	.var SizeVal = length.getValue()
	.var SourceVal = DMASource.getValue()
	.var DestinationVal = DMADestination.getValue()
	
	.var IsChained = false
	.if (chained.getType() != AT_NONE) {
		.eval IsChained = true
	}

	.var MarkerVal = $03
	.if (IsChained) {
		.eval MarkerVal = $07
	}

		.byte $00			// no more options
		.byte MarkerVal
		.word SizeVal
		.word [SourceVal & $ffff]
		.byte $00
		.word [DestinationVal & $ffff]
		.byte [[DestinationVal >> 16] & $0f]

		.if (IsChained) {
			.word $0000
		}
}

//-----------------------------------------------------------------------------
// Defines DMA source stepping values.
//
// Parameters:
// - step the step DMA should use for source value
.pseudocommand DMA_JobStepSource step
{
		.if (step.getType() != AT_NONE) {
			.byte $82, <step.getValue()
			.byte $83, >step.getValue()
		}
}

//-----------------------------------------------------------------------------
// Defines DMA destination stepping values.
//
// Parameters:
// - step the step DMA should use for destination value
.pseudocommand DMA_JobStepDest step
{
		.if (step.getType() != AT_NONE) {
			.byte $84, <step.getValue()
			.byte $85, >step.getValue()
		}
}

//-----------------------------------------------------------------------------
// Disables DMA transparency
.pseudocommand DMA_JobDisableTransparency
{
		.byte $06
}


//-----------------------------------------------------------------------------
// Enables DMA transparency.
//
// Parameters:
// - transparentByte the value that defined transparency
.pseudocommand DMA_JobEnableTransparency transparentByte
{
		.byte $07
		.byte $86
		.byte transparentByte
}

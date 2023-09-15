$CONVERT = "M65Converter\bin\Debug\net7.0\M65Converter.exe"

$OPT_COMMON = "--verbosity info --screen-address 0x10000 --chars-address 0x20000"

$IN_PATH = "UnitTests\Resources"
$OUT_PATH = $IN_PATH
$COLOURS = "fcm", "ncm"

function Run-Converter
{
	param
	(
		[string]$Colour,
		[bool]$IsBaseChars=$false,
		[bool]$IsScreens=$false,
		[bool]$IsRRB=$false
	)

	# First we need to establish export file name prefix
	$OUT_PREFIX = "$OUT_PATH\export-$Colour"

	$BASE_CHARS = ""
	if ($IsBaseChars)
	{
		$OUT_PREFIX = "$($OUT_PREFIX)+base"
	}

	$SCREENS = ""
	if ($IsScreens) {
		$OUT_PREFIX = "$($OUT_PREFIX)+screens"
	}

	$RRB = ""
	if ($IsRRB)
	{
		$RRB = "--rrb"
		$OUT_PREFIX = "$($OUT_PREFIX)+rrb"
	}

	# Prepare cmd line options for chars. We always generate output palette and chars, but usage of base chars is optional
	$OPT_CHARS = "chars --out-palette $OUT_PREFIX-palette.pal --out-chars $OUT_PREFIX-chars.bin"
	if ($IsBaseChars)
	{
		$OPT_CHARS = "$OPT_CHARS $IN_PATH\input-base-chars.png"
	}

	# Prepare cmd line options for screens. We only use screens if the options is provided.
	$OPT_SCREENS = ""
	if ($IsScreens)
	{
		$OPT_SCREENS = "screens $RRB --out-screen $OUT_PREFIX-screen.bin --out-colour $OUT_PREFIX-colour.bin --out-lookup $OUT_PREFIX-lookup.bin $IN_PATH\input-level.aseprite"
	}

	Invoke-Expression "$CONVERT $OPT_COMMON --colour $Colour $BASE_CHARS $SCREENS $OPT_CHARS $OPT_SCREENS"
}

function Run-Colour
{
	param
	(
		[bool]$IsRRB=$false
	)

	for ($i = 0; $i -lt $COLOURS.Count; $i++)
	{
		Run-Converter -Colour $COLOURS[$i] -IsBaseChars $false -IsScreens $false -IsRRB $IsRRB
		Run-Converter -Colour $COLOURS[$i] -IsBaseChars $false -IsScreens $true -IsRRB $IsRRB
		Run-Converter -Colour $COLOURS[$i] -IsBaseChars $true -IsScreens $false -IsRRB $IsRRB
		Run-Converter -Colour $COLOURS[$i] -IsBaseChars $true -IsScreens $true -IsRRB $IsRRB
	}
}

Run-Colour -IsRRB $false
Run-Colour -IsRRB $true

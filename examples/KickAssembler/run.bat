@echo off

rem CHANGE THE PATH TO YOUR KICK ASSEMBLER AND OTHER TOOLS BELOW!

set M65CONVERTER=..\M65Converter\bin\Debug\net7.0\M65Converter
set KICK=java -cp ..\..\..\development\compiler\Kick\KickAss65CE02.jar kickass.KickAssembler65CE02
set XEMU=..\..\..\emulator\Xemu\xmega65

rem ^^^^^^^^^ probably no need to change anything below ^^^^^^^^^

echo Converting assets

%M65CONVERTER% ^
    --verbosity verbose ^
    --colour ncm ^
    --info 2 ^
    --screen-address $10000 ^
    --chars-address $20000 ^
    chars ^
    --out-chars build\assets\chars.bin ^
    --out-palette build\assets\chars.pal ^
    screens ^
    --out-screen build\assets\level\screen.bin ^
    --out-colour build\assets\level\colour.bin ^
    --out-lookup build\assets\level\lookup.inf ^
    --out-info build\assets\level\info.png ^
    assets\ldtk\level\simplified\AutoLayer ^
    rrbsprites ^
    --append-screens ^
    --position 87,-15 130,2 10,30 ^
    --out-frames build\assets\spr-{name}.bin ^
    --out-lookup build\assets\spr-{name}.inf ^
    assets\aseprite\player-idle.aseprite ^
    assets\aseprite\player-run.aseprite

echo Assembling

%KICK% ^
    src\main.s ^
    -odir ..\build ^
    -showmem ^
    -debugdump ^
    -bytedumpfile build\ByteDump.txt ^
    -vicesymbols ^
    -symbolfile

echo Running in emulator

%XEMU% -besure -prg build\main.prg

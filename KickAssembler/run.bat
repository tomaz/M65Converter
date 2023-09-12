@echo off

rem CHANGE THE PATH TO YOUR KICK ASSEMBLER AND OTHER TOOLS BELOW!

set M65CONVERTER=..\M65Converter\bin\Debug\net7.0\M65Converter
set KICK=java -cp ..\..\..\development\compiler\Kick\KickAss65CE02.jar kickass.KickAssembler65CE02
set XEMU=..\..\..\emulator\Xemu\xmega65

rem ^^^^^^^^^ probably no need to change anything below ^^^^^^^^^

echo Converting assets

%M65CONVERTER% -v verbose --info 2 --colour ncm chars --out-chars build\assets\chars.bin --out-palette build\assets\chars.pal screens --screen-address $10000 --chars-address $20000 --out-screen build\assets\{level}\screen.bin --out-colour build\assets\{level}\colour.bin --out-lookup build\assets\{level}\lookup.inf --out-info build\assets\{level}\info.png assets\ldtk\level\simplified\AutoLayer

echo Assembling

%KICK% src\main.s -odir ..\build -showmem -debugdump -bytedumpfile build\ByteDump.txt -vicesymbols -symbolfile

echo Running in emulator

%XEMU% -besure -prg build\main.prg

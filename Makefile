SHELL := pwsh.exe
.SHELLFLAGS := -NoProfile -Command

TARGETS := win-x64 win-x86 linux-x64 osx-x64 osx-arm64

define newline


endef

.PHONY: publish

publish:
	$(foreach TARGET,$(TARGETS), \
		dotnet publish --self-contained false --configuration Release --framework net7.0 --runtime $(TARGET) $(newline) \
		Copy-Item -Path Fonts\*.ttf -Destination M65Converter\bin\Release\net7.0\$(TARGET)\publish\ $(newline) \
		Compress-Archive -Path .\M65Converter\bin\Release\net7.0\$(TARGET)\publish\*.* -DestinationPath M65Converter\bin\Release\M65Converter-$(TARGET).zip $(newline) \
	)

clean:
	Remove-Item -Force -Recurse -Path M65Converter\bin\Release



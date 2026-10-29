$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AppearanceAssets.cs'),[Text.Encoding]::UTF8)) -ReferencedAssemblies System.Drawing
[TappoHelper.Art.AssetBuilder]::Save((Join-Path $PSScriptRoot 'assets'))

; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

; A release header takes a plain version number, so the 1.0.0 previews are listed under 1.0.0.
; E5001 first shipped in package 1.0.0-preview-0064.
## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E5001 | EastFive.Storage | Warning | StorageTableETagAnalyzer

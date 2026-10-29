Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $type) { $m = $asTask.MakeGenericMethod($type); $t = $m.Invoke($null, @($op)); $t.Wait(-1) | Out-Null; $t.Result }
$asAction = ([System.WindowsRuntimeSystemExtensions].GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncActionWithProgress`1' })[0]
[void][Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
[void][Windows.Storage.StorageFolder, Windows.Storage, ContentType=WindowsRuntime]
[void][Windows.Media.Transcoding.MediaTranscoder, Windows.Media.Transcoding, ContentType=WindowsRuntime]
[void][Windows.Media.MediaProperties.MediaEncodingProfile, Windows.Media.MediaProperties, ContentType=WindowsRuntime]
$src = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('C:\Users\drago\Downloads\Travis Scott and Fortnite Present Astronomical (Full Event Video).mp3')) ([Windows.Storage.StorageFile])
$folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync('C:\Users\drago\Documents\AstronomicalEventMod\tools')) ([Windows.Storage.StorageFolder])
$dst = Await ($folder.CreateFileAsync('event.mp3', [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
$prof = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateMp3([Windows.Media.MediaProperties.AudioEncodingQuality]::High)
$tc = New-Object Windows.Media.Transcoding.MediaTranscoder
$tc.TrimStartTime = [TimeSpan]::FromSeconds(28); $tc.TrimStopTime = [TimeSpan]::FromSeconds(529)
$prep = Await ($tc.PrepareFileTranscodeAsync($src, $dst, $prof)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
"CanTranscode=" + $prep.CanTranscode + " reason=" + $prep.FailureReason
if ($prep.CanTranscode) { $m = $asAction.MakeGenericMethod([double]); $t = $m.Invoke($null, @($prep.TranscodeAsync())); $t.Wait(-1) | Out-Null; "done" }

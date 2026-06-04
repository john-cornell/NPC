Get-ChildItem -Path C:\Code\NPC\NPC.Village\Behaviors -Recurse -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $nc = $c -replace '\(int X, int Y\)', '(int X, int Y, int Z)'
    $nc = $nc -replace '\(int, int\)', '(int X, int Y, int Z)'
    if ($c -ne $nc) { Set-Content -Path $_.FullName -Value $nc }
}

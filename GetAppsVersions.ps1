param(
  [string]$DevRoot = "C:\inetpub\wwwroot\Development",
  [string]$StaRoot = "C:\inetpub\wwwroot\Staging",
  [string]$ProRoot = "C:\inetpub\wwwroot\Production",

  # Nombre del ensamblado secundario común presente en todas las apps
  [string]$CommonAssemblyName = "Company.Common.dll",

  # Profundidad de búsqueda dentro de cada carpeta de app (para localizar el común)
  [int]$SearchDepth = 4
)

function Get-FileVersionInfoSafe {
  param([string]$Path)
  try {
    $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    # ProductVersion suele reflejar la versión “informacional”; FileVersion la de archivo
    $ver = if ($fvi.ProductVersion) { $fvi.ProductVersion } elseif ($fvi.FileVersion) { $fvi.FileVersion } else { "" }
    return $ver
  } catch { return "" }
}

function Find-MainAssembly {
  param([string]$AppDir)

  # 1) Preferir EXE (si coincide con nombre de carpeta, mejor)
  $exe = Get-ChildItem -Path $AppDir -Filter *.exe -File -ErrorAction SilentlyContinue
  if ($exe) {
    $folderName = Split-Path $AppDir -Leaf
    $exeExact = $exe | Where-Object { $_.BaseName -ieq $folderName } | Select-Object -First 1
    if ($exeExact) { return $exeExact.FullName }
    return ($exe | Select-Object -First 1).FullName
  }

  # 2) DLL con runtimeconfig/deps (publicación .NET)
  $dll = Get-ChildItem -Path $AppDir -Filter *.dll -File -ErrorAction SilentlyContinue
  if ($dll) {
    $folderName = Split-Path $AppDir -Leaf
    $dllWithConfig = $dll | Where-Object {
      Test-Path (Join-Path $_.DirectoryName ($_.BaseName + ".runtimeconfig.json")) -or
      Test-Path (Join-Path $_.DirectoryName ($_.BaseName + ".deps.json"))
    }
    $dllExact = $dllWithConfig | Where-Object { $_.BaseName -ieq $folderName } | Select-Object -First 1
    if ($dllExact) { return $dllExact.FullName }
    if ($dllWithConfig) { return ($dllWithConfig | Select-Object -First 1).FullName }
    # 3) Si no, el DLL más grande
    return ($dll | Sort-Object Length -Descending | Select-Object -First 1).FullName
  }

  return $null
}

function Find-CommonAssembly {
  param([string]$AppDir, [string]$AssemblyName, [int]$Depth)

  # Búsqueda limitada por profundidad (para evitar recorrer node_modules, logs, etc.)
  # Recorremos de manera iterativa por niveles
  $queue = @([PSCustomObject]@{Path=$AppDir; Level=0})
  while ($queue.Count -gt 0) {
    $current = $queue[0]; $queue = $queue[1..($queue.Count-1)]
    try {
      $files = Get-ChildItem -Path $current.Path -File -ErrorAction SilentlyContinue
      $hit = $files | Where-Object { $_.Name -ieq $AssemblyName } | Select-Object -First 1
      if ($hit) { return $hit.FullName }

      if ($current.Level -lt $Depth) {
        $dirs = Get-ChildItem -Path $current.Path -Directory -ErrorAction SilentlyContinue
        foreach ($d in $dirs) {
          # Omitir carpetas típicas irrelevantes
          if ($d.Name -in @("logs","log","temp","tmp")) { continue }
          $queue += [PSCustomObject]@{Path=$d.FullName; Level=$current.Level + 1}
        }
      }
    } catch {}
  }
  return $null
}

function Get-AppInfo {
  param([string]$AppDir, [string]$CommonAssemblyName, [int]$Depth)

  $main = Find-MainAssembly -AppDir $AppDir
  if (-not $main) {
    return $null
  }

  $common = Find-CommonAssembly -AppDir $AppDir -AssemblyName $CommonAssemblyName -Depth $Depth

  $mainVer   = Get-FileVersionInfoSafe -Path $main
  $commonVer = if ($common) { Get-FileVersionInfoSafe -Path $common } else { "" }
  $deploy    = (Get-Item $main).LastWriteTime

  return [PSCustomObject]@{
    AppName           = (Split-Path $AppDir -Leaf)
    MainAssemblyPath  = $main
    MainVersion       = $mainVer
    CommonAssembly    = $CommonAssemblyName
    CommonAssemblyPath= $common
    CommonVersion     = $commonVer
    DeployDate        = $deploy
  }
}

function Scan-Environment {
  param([string]$Root, [string]$EnvName, [string]$CommonAssemblyName, [int]$Depth)

  if (-not (Test-Path $Root)) { return @{} }

  $result = @{}
  $subdirs = Get-ChildItem -Path $Root -Directory -ErrorAction SilentlyContinue
  foreach ($d in $subdirs) {
    $info = Get-AppInfo -AppDir $d.FullName -CommonAssemblyName $CommonAssemblyName -Depth $Depth
    if ($info) {
      $result[$info.AppName] = $info
    }
  }
  return $result
}

# --- Escaneo de los 3 entornos ---
$devMap = Scan-Environment -Root $DevRoot -EnvName "DEV" -CommonAssemblyName $CommonAssemblyName -Depth $SearchDepth
$staMap = Scan-Environment -Root $StaRoot -EnvName "STA" -CommonAssemblyName $CommonAssemblyName -Depth $SearchDepth
$proMap = Scan-Environment -Root $ProRoot -EnvName "PRO" -CommonAssemblyName $CommonAssemblyName -Depth $SearchDepth

# Conjunto de todas las apps detectadas
$appNames = @($devMap.Keys + $staMap.Keys + $proMap.Keys) | Sort-Object -Unique

# Construir tabla final con columnas DEV/STA/PRO
$rows = @()
foreach ($name in $appNames) {
  $fmt = {
    param($info)
    if (-not $info) { return "" }
    $date = $info.DeployDate.ToString("yyyy-MM-dd HH:mm")
    $main = if ($info.MainVersion) { $info.MainVersion } else { "(sin versión)" }
    $common = if ($info.CommonVersion) { $info.CommonVersion } else { "no encontrado" }
    # Texto multilínea por celda
    return "Main: $main`nCommon: $common`nFecha: $date"
  }

  $rows += [PSCustomObject]@{
    App = $name
    DEV = & $fmt $devMap[$name]
    STA = & $fmt $staMap[$name]
    PRO = & $fmt $proMap[$name]
  }
}

# Salida en consola como tabla
$rows | Format-Table -AutoSize

# Exportaciones útiles
$csvPath = Join-Path (Get-Location) "AppsInventario.csv"
$mdPath  = Join-Path (Get-Location) "AppsInventario.md"
$rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

# Exportar también en Markdown para documentación/pegado rápido
$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("| App | DEV | STA | PRO |")
[void]$md.AppendLine("|---|---|---|---|")
foreach ($r in $rows) {
  # Reemplazar saltos de línea por <br> para celdas Markdown
  $dev = ($r.DEV -replace "`r?`n","<br>")
  $sta = ($r.STA -replace "`r?`n","<br>")
  $pro = ($r.PRO -replace "`r?`n","<br>")
  [void]$md.AppendLine("| $($r.App) | $dev | $sta | $pro |")
}
$md.ToString() | Out-File -FilePath $mdPath -Encoding UTF8

Write-Host ""
Write-Host "Inventario generado:"
Write-Host " - CSV: $csvPath"
Write-Host " - MD : $mdPath"
Write-Host ""
Write-Host "Sugerencia: prueba con -CommonAssemblyName ""Company.Common.dll"" si tu ensamblado común tiene otro nombre."

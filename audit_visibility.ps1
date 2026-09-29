<#
.SYNOPSIS
  Visibility audit: checks that Disguise.dll does not call any member that is
  inaccessible in the game's (original) assembly.

.DESCRIPTION
  The project compiles against valheim_Data\Managed\publicized_assemblies\*.dll, where EVERY
  member is made public. The game loads the original assembly instead, where private /
  protected members stay as they are: a direct reference from our DLL then throws a
  FieldAccessException / MethodAccessException at runtime
  (e.g. "Field `Terminal:commands' is inaccessible").

  This script re-reads the produced DLL with Mono.Cecil, lists every game member it
  references, resolves it in the ORIGINAL assembly and reports the ones that are not public
  -> they must be replaced with reflection (AccessTools / FieldInfo / MethodInfo).

  Run it after every build: the "Deploy" target of the .csproj copies the DLL into the Gale
  profile "dev" (BepInEx\scripts).

.EXAMPLE
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\audit_visibility.ps1
#>

param(
  [string]$Dll = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\dev\BepInEx\scripts\Disguise.dll",
  [string]$GamePath = "A:\Steam\steamapps\common\Valheim"
)

$managed = Join-Path $GamePath "valheim_Data\Managed"
$core    = Join-Path $GamePath "BepInEx\core"

if (-not (Test-Path $Dll))      { Write-Error "DLL not found: $Dll"; exit 1 }
if (-not (Test-Path $GamePath)) { Write-Error "Game not found: $GamePath"; exit 1 }

Add-Type -AssemblyName System.Core
[System.Reflection.Assembly]::LoadFrom((Join-Path $core "Mono.Cecil.dll")) | Out-Null

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$resolver.AddSearchDirectory($core)
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $resolver
$rp.ReadSymbols = $false

$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Dll, $rp).MainModule
$gameAssemblies = @("assembly_valheim", "assembly_utils")

function Find-Field([Mono.Cecil.TypeDefinition]$t, [string]$name) {
  while ($t -ne $null) {
    foreach ($f in $t.Fields) { if ($f.Name -eq $name) { return $f } }
    try { $t = $t.BaseType.Resolve() } catch { $t = $null }
  }
  return $null
}
function Find-Method([Mono.Cecil.TypeDefinition]$t, [string]$name) {
  $found = @()
  while ($t -ne $null) {
    foreach ($m in $t.Methods) { if ($m.Name -eq $name) { $found += $m } }
    if ($found.Count -gt 0) { return $found }
    try { $t = $t.BaseType.Resolve() } catch { $t = $null }
  }
  return $found
}
function Type-IsVisible([Mono.Cecil.TypeDefinition]$t) {
  while ($t -ne $null) {
    if (-not ($t.IsPublic -or $t.IsNestedPublic -or $t.IsNestedFamily -or $t.IsNestedFamORAssem)) { return $false }
    $t = $t.DeclaringType
  }
  return $true
}
function Visibility([Mono.Cecil.IMemberDefinition]$m) {
  if ($m.IsPublic) { "public" } elseif ($m.IsAssembly) { "internal" } elseif ($m.IsFamily) { "protected" }
  elseif ($m.IsFamilyORAssem) { "protected internal" } elseif ($m.IsFamilyAndAssembly) { "private protected" }
  elseif ($m.IsPrivate) { "private" } else { "?" }
}

$checked = 0
$issues  = 0
foreach ($mr in $mod.GetMemberReferences()) {
  $scope = $mr.DeclaringType.Scope
  $asmName = $null
  if     ($scope -is [Mono.Cecil.AssemblyNameReference]) { $asmName = $scope.Name }
  elseif ($scope -is [Mono.Cecil.ModuleDefinition])      { $asmName = $scope.Assembly.Name.Name }
  if ($gameAssemblies -notcontains $asmName) { continue }
  $checked++

  try { $t = $mr.DeclaringType.Resolve() } catch { $t = $null }
  if ($t -eq $null) { Write-Output "  NOT FOUND      : $($mr.FullName)"; $issues++; continue }
  if (-not (Type-IsVisible $t)) { Write-Output "  NON PUBLIC TYPE: $($t.FullName)  (used by $($mr.FullName))"; $issues++; continue }

  if ($mr -is [Mono.Cecil.FieldReference]) {
    $f = Find-Field $t $mr.Name
    if ($f -eq $null)         { Write-Output "  FIELD NOT FOUND: $($mr.FullName)"; $issues++ }
    elseif (-not $f.IsPublic) { Write-Output "  FIELD $(Visibility $f) : $($t.FullName)::$($f.Name)"; $issues++ }
  } else {
    $ms = Find-Method $t $mr.Name
    if ($ms.Count -eq 0) { Write-Output "  METHOD NOT FOUND: $($mr.FullName)"; $issues++; continue }
    foreach ($m in $ms) {
      if (-not $m.IsPublic) {
        Write-Output "  METHOD $(Visibility $m) : $($t.FullName)::$($m.Name)($($m.Parameters.Count))"
        $issues++
      }
    }
  }
}

Write-Output ""
Write-Output "Game members referenced: $checked   ISSUES: $issues"
if ($issues -gt 0) {
  Write-Output "-> Replace every listed access with reflection (FieldInfo / MethodInfo / AccessTools)."
  exit 2
}
exit 0

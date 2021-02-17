Param([string] $Path, [string] $Destination)

$ErrorActionPreference = "Stop"

$excludes = @("bin" ; "obj" ; "*.cs" ; "feed")
$doc = [xml]::new()
$doc.AppendChild($doc.CreateXmlDeclaration("1.0", "UTF-8", $null)) >$null

function Import-Children([string] $subpath, [System.Xml.XmlNode] $node) {
    write-host "Looking at $subpath"
    foreach($item in Get-ChildItem $subpath -Exclude $excludes) {
        $child = $node.AppendChild($doc.CreateElement($item.PSChildName))

        if ($item.PSIsContainer) {
            Import-Children $item.FullName $child
        } else {
            $child.AppendChild($doc.CreateCDataSection((Get-Content -Raw $item))) >$null
        }
    }
}

Import-Children -subpath $Path -node ($doc.AppendChild($doc.CreateElement((Get-Item $Path).Name)))

$doc.Save((Join-Path $pwd $Destination)) >$null
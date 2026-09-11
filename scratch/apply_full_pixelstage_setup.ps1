$operatingPath = "c:\unityProject\Cashier\Assets\Prefabs\GameUI\OperatingPanel.prefab"
$gameUiPath = "c:\unityProject\Cashier\Assets\Prefabs\GameUI\GameUI.prefab"

# 1. Update GameUI.prefab stripped references
Write-Host "Updating GameUI.prefab stripped references..."
$gameUiContent = [System.IO.File]::ReadAllText($gameUiPath, [System.Text.Encoding]::UTF8)

# Replace 5638334816923447982 with 3421886702833186749 (AstraFrontView)
$gameUiContent = $gameUiContent.Replace(
    "m_CorrespondingSourceObject: {fileID: 5638334816923447982, guid: cb56396611f15064f9ce66b932c05550, type: 3}",
    "m_CorrespondingSourceObject: {fileID: 3421886702833186749, guid: cb56396611f15064f9ce66b932c05550, type: 3}"
)

# Replace 2288817115886397551 with 8277017469611850638 (FrontContainer Button)
$gameUiContent = $gameUiContent.Replace(
    "m_CorrespondingSourceObject: {fileID: 2288817115886397551, guid: cb56396611f15064f9ce66b932c05550, type: 3}",
    "m_CorrespondingSourceObject: {fileID: 8277017469611850638, guid: cb56396611f15064f9ce66b932c05550, type: 3}"
)

[System.IO.File]::WriteAllText($gameUiPath, $gameUiContent, [System.Text.Encoding]::UTF8)
Write-Host "GameUI.prefab updated successfully."

# 2. Update OperatingPanel.prefab
Write-Host "Updating OperatingPanel.prefab..."
$content = [System.IO.File]::ReadAllText($operatingPath, [System.Text.Encoding]::UTF8)

# Split by document
$regex = [regex]"(?m)^--- !u!(\d+) &(\d+)\r?\n"
$matches = $regex.Matches($content)

$docs = [System.Collections.Generic.Dictionary[string, string]]::new()
$docTypes = [System.Collections.Generic.Dictionary[string, string]]::new()
$docOrder = [System.Collections.Generic.List[string]]::new()

for ($i = 0; $i -lt $matches.Count; $i++) {
    $m = $matches[$i]
    $type = $m.Groups[1].Value
    $id = $m.Groups[2].Value
    $startIdx = $m.Index
    $endIdx = if ($i + 1 -lt $matches.Count) { $matches[$i + 1].Index } else { $content.Length }
    $body = $content.Substring($startIdx, $endIdx - $startIdx)
    $docs[$id] = $body
    $docTypes[$id] = $type
    $docOrder.Add($id)
}

# IDs to delete (old SkyBirds)
$skyBirdsIds = @("9196041898650300023", "1262450746076122215", "4820548954396463814", "7570893578749699753")
foreach ($id in $skyBirdsIds) {
    if ($docs.ContainsKey($id)) {
        [void]$docs.Remove($id)
        [void]$docOrder.Remove($id)
    }
}

# Update LeftWatchGuard Image 512834876170757772
if ($docs.ContainsKey("512834876170757772")) {
    $doc = $docs["512834876170757772"]
    $doc = $doc -replace "m_PreserveAspect: 0", "m_PreserveAspect: 1"
    $doc = $doc -replace "m_Material: \{fileID: 0\}", "m_Material: {fileID: 2100000, guid: fd2f3438ec99b4d4c9799a4f814dce20, type: 2}"
    $docs["512834876170757772"] = $doc
}

# Update RightWatchGuard Image 3885454632951923915
if ($docs.ContainsKey("3885454632951923915")) {
    $doc = $docs["3885454632951923915"]
    $doc = $doc -replace "m_PreserveAspect: 0", "m_PreserveAspect: 1"
    $doc = $doc -replace "m_Material: \{fileID: 0\}", "m_Material: {fileID: 2100000, guid: fd2f3438ec99b4d4c9799a4f814dce20, type: 2}"
    $docs["3885454632951923915"] = $doc
}

# Update AstraFrontView GameObject 3421886702833186749 components
$astraGoDoc = @"
--- !u!1 &3421886702833186749
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1254427590675692130}
  - component: {fileID: 9200000000000000071}
  - component: {fileID: 9200000000000000081}
  - component: {fileID: 1316934037393887661}
  m_Layer: 0
  m_Name: AstraFrontView
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"@
$docs["3421886702833186749"] = $astraGoDoc.TrimEnd() + "`r`n"

# Update AstraFrontView RectTransform 1254427590675692130 children
$astraRtDoc = @"
--- !u!224 &1254427590675692130
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 3421886702833186749}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 6349070201352829346}
  - {fileID: 9200000000000000002}
  - {fileID: 9200000000000000012}
  - {fileID: 9200000000000000022}
  - {fileID: 9200000000000000032}
  - {fileID: 5679084557932859917}
  - {fileID: 225792238029537004}
  - {fileID: 5125004635812788921}
  - {fileID: 8638597772381084482}
  - {fileID: 3314995927231685298}
  - {fileID: 5223773053038289920}
  - {fileID: 6562592933121663812}
  - {fileID: 7569499308122249970}
  - {fileID: 2685760390977519454}
  - {fileID: 7936594199967781}
  - {fileID: 405462191345195272}
  - {fileID: 4929980727901594393}
  - {fileID: 9192111449977608784}
  - {fileID: 7404721791512357773}
  - {fileID: 8908548568498727313}
  - {fileID: 6850923239564261770}
  - {fileID: 3606819251570704977}
  - {fileID: 3867398379584970586}
  - {fileID: 6592366221705012239}
  - {fileID: 9200000000000000042}
  - {fileID: 9200000000000000052}
  - {fileID: 3720790236600243018}
  - {fileID: 9200000000000000062}
  - {fileID: 6224414121057216258}
  - {fileID: 1976275620682369353}
  - {fileID: 7276000497302158311}
  - {fileID: 3005097993321621703}
  m_Father: {fileID: 1659132799136591793}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}
"@
$docs["1254427590675692130"] = $astraRtDoc.TrimEnd() + "`r`n"

# Define the new documents to add
$newYamlDocs = @"
--- !u!1 &9200000000000000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000002}
  - component: {fileID: 9200000000000000003}
  - component: {fileID: 9200000000000000004}
  m_Layer: 0
  m_Name: DawnBackground
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000002
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000001}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 1280, y: 720}
  m_Pivot: {x: 0, y: 1}
--- !u!222 &9200000000000000003
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000001}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: b8216f62abea4584bba1dc368f809003, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &9200000000000000011
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000012}
  - component: {fileID: 9200000000000000013}
  - component: {fileID: 9200000000000000014}
  m_Layer: 0
  m_Name: SunsetBackground
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000012
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000011}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 1280, y: 720}
  m_Pivot: {x: 0, y: 1}
--- !u!222 &9200000000000000013
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000011}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000014
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000011}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 04a9ae531c12e4b47a00f07be2b497c9, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &9200000000000000021
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000022}
  - component: {fileID: 9200000000000000023}
  - component: {fileID: 9200000000000000024}
  m_Layer: 0
  m_Name: EveningBackground
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000022
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000021}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 1280, y: 720}
  m_Pivot: {x: 0, y: 1}
--- !u!222 &9200000000000000023
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000021}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000024
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000021}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 2bbf3ae139912e04ea4029df3f445650, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &9200000000000000031
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000032}
  - component: {fileID: 9200000000000000033}
  - component: {fileID: 9200000000000000034}
  m_Layer: 0
  m_Name: CityLights
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000032
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000031}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 1280, y: 720}
  m_Pivot: {x: 0, y: 1}
--- !u!222 &9200000000000000033
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000031}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000034
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000031}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 2100000, guid: 8170df3e7f262d04c82832f5dc01de8c, type: 2}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: bf83a4448d216984aa86bb2759c4260a, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &9200000000000000041
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000042}
  - component: {fileID: 9200000000000000043}
  - component: {fileID: 9200000000000000044}
  m_Layer: 0
  m_Name: LeftBeam
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000042
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000041}
  m_LocalRotation: {x: 0, y: 0, z: -0.06684784, w: 0.9977631}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: -7.67}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 110, y: -164}
  m_SizeDelta: {x: 1800, y: 360}
  m_Pivot: {x: 0, y: 0.5}
--- !u!222 &9200000000000000043
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000041}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000044
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000041}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: f85fe24aaf7cacb4ba83f06c29b017cd, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &9200000000000000051
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000052}
  - component: {fileID: 9200000000000000053}
  - component: {fileID: 9200000000000000054}
  m_Layer: 0
  m_Name: RightBeam
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000052
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000051}
  m_LocalRotation: {x: 0, y: 0, z: 0.9984951, w: -0.05484218}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 186.29}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 1140, y: -156}
  m_SizeDelta: {x: 1800, y: 366.67}
  m_Pivot: {x: 0, y: 0.5}
--- !u!222 &9200000000000000053
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000051}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000054
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000051}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: f85fe24aaf7cacb4ba83f06c29b017cd, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &9200000000000000061
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 9200000000000000062}
  - component: {fileID: 9200000000000000063}
  - component: {fileID: 9200000000000000064}
  m_Layer: 0
  m_Name: CounterLight
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &9200000000000000062
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000061}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1254427590675692130}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 1}
  m_AnchorMax: {x: 0, y: 1}
  m_AnchoredPosition: {x: 355, y: -455}
  m_SizeDelta: {x: 600, y: 180}
  m_Pivot: {x: 0, y: 1}
--- !u!222 &9200000000000000063
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000061}
  m_CullTransparentMesh: 1
--- !u!114 &9200000000000000064
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9200000000000000061}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 1462c53c46cb05f42a469c35cabf9b2c, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &9200000000000000071
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 3421886702833186749}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 6970932de48d45940a94a01e14e21cea, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::TimeOfDayPixelStage
  frontCanvas: {fileID: 1254427590675692130}
  layers:
  - source: {fileID: 2867742435294450687}
    surface: 7
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000004}
    surface: 7
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000014}
    surface: 7
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000024}
    surface: 7
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000034}
    surface: 4
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9089328896964645706}
    surface: 5
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 4302888746661235200}
    surface: 5
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 4487786938158414642}
    surface: 5
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 3383462122338495752}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 4608225442380465825}
    surface: 5
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 5492949787792291801}
    surface: 3
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 5526285277847546751}
    surface: 3
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 3265061915938457750}
    surface: 3
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 7510516729275094552}
    surface: 3
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 512834876170757772}
    surface: 2
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 3885454632951923915}
    surface: 2
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 8725881023729905250}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 1032693613676253156}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 3387655548195392710}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 5893512099457146213}
    surface: 5
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 8994819866920577612}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 1026733735570050992}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0.2
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000044}
    surface: 0
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000054}
    surface: 0
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 3554605918309477532}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0.3
    lampResponse: 0.15
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 9200000000000000064}
    surface: 0
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 7602069853485281819}
    surface: 2
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 1
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 1746331491081561778}
    surface: 1
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0
    lampResponse: 0.6
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  - source: {fileID: 6147561814899790376}
    surface: 3
    normalSprite: {fileID: 0}
    normalMap: {fileID: 0}
    roomResponse: 0.5
    lampResponse: 1
    bottomShade: 0
    contactShadow: {x: 0, y: 0, z: 0, w: 0}
  lightingShader: {fileID: 4800000, guid: 7354e1219b89b7749a39d467e664b8ff, type: 3}
  width: 480
  previewInEditor: 1
  lampPosition: {x: 700, y: 290}
  lampHeight: 240
  lampRadius: 520
  lampIntensity: 3.2
  lampColor: {r: 1, g: 0.76, b: 0.46, a: 1}
  lightingSteps: 6
  rimIntensity: 1.3
  dawnFog: {r: 1, g: 0.84, b: 0.67, a: 1}
  sunsetFog: {r: 1, g: 0.54, b: 0.28, a: 1}
  nightFog: {r: 0.28, g: 0.39, b: 0.59, a: 1}
  skyGlowIntensity: 0.3
  daylightContrast: 0.65
  sunsetContrast: 0.8
  daylightShadowOpacity: 0.3
  sunsetShadowOpacity: 0.55
  hourlyAmbient:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlySunlight:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlyNormal:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlyHardness:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlyFill:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 0.15
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 0.15
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlyShadow:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlySunX:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  hourlySunY:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 9
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    - serializedVersion: 3
      time: 21
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0
      outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  relightingTrial: 1
  useCustomerNormalMap: 1
  normalStrength: 0.65
  propNormalStrength: 1
  propNightFill: 0.3
  roomLightStrength: 1.2
  eveningSpotlight: 1
  spotOrigin: {x: 1050, y: -60}
  spotTarget: {x: 600, y: 600}
  spotIntensity: 4.5
  spotHalfAngle: 21
  spotHaze: 0.025
  towerBacklight: 3.2
  customerShadowOpacity: 0.65
  shadowTableY: {x: 520, y: 625}
--- !u!114 &9200000000000000081
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 3421886702833186749}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3e3dd6432d4dee04792eb9f647761ca5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Cashier.Runtime::TimeOfDayUIController
  businessClock: {fileID: 5584353875346234155}
  dawn: {fileID: 9200000000000000004}
  sunset: {fileID: 9200000000000000014}
  evening: {fileID: 9200000000000000024}
  cityLights: {fileID: 9200000000000000034}
  counterLight: {fileID: 9200000000000000064}
  leftBeam: {fileID: 9200000000000000044}
  rightBeam: {fileID: 9200000000000000054}
  dawnSprite: {fileID: 21300000, guid: b8216f62abea4584bba1dc368f809003, type: 3}
  sunsetSprite: {fileID: 21300000, guid: 04a9ae531c12e4b47a00f07be2b497c9, type: 3}
  eveningSprite: {fileID: 21300000, guid: 2bbf3ae139912e04ea4029df3f445650, type: 3}
  cityLightsSprite: {fileID: 21300000, guid: bf83a4448d216984aa86bb2759c4260a, type: 3}
  counterLightSprite: {fileID: 21300000, guid: 1462c53c46cb05f42a469c35cabf9b2c, type: 3}
  searchlightSprite: {fileID: 21300000, guid: f85fe24aaf7cacb4ba83f06c29b017cd, type: 3}
  cityLightsMaterial: {fileID: 2100000, guid: 8170df3e7f262d04c82832f5dc01de8c, type: 2}
  environment:
  - {fileID: 2867742435294450687}
  - {fileID: 3383462122338495752}
  - {fileID: 8725881023729905250}
  - {fileID: 1032693613676253156}
  - {fileID: 3387655548195392710}
  - {fileID: 5492949787792291801}
  - {fileID: 5526285277847546751}
  - {fileID: 3265061915938457750}
  - {fileID: 7510516729275094552}
  - {fileID: 8994819866920577612}
  - {fileID: 1026733735570050992}
  - {fileID: 3554605918309477532}
  - {fileID: 4302888746661235200}
  - {fileID: 4487786938158414642}
  - {fileID: 512834876170757772}
  - {fileID: 3885454632951923915}
  people:
  - {fileID: 7602069853485281819}
  dawnStart: 9
  dayStart: 12
  sunsetStart: 15
  eveningStart: 18
  nightEnd: 21
  dawnTint: {r: 1, g: 0.9, b: 0.8, a: 1}
  sunsetTint: {r: 1, g: 0.72, b: 0.49, a: 1}
  nightTint: {r: 0.38, g: 0.43, b: 0.56, a: 1}
  peopleBrightness: 0.72
  cityIntensity: 0.7
  beamIntensity: 0.11
  counterIntensity: 0.12
  pixelStage: {fileID: 9200000000000000071}
  debugOverrideTime: 0
  debugHour: 9
  autoAdvanceClockForTesting: 0
  fullCycleSeconds: 45
  showDebugOverlay: 1
  previewInEditor: 0
  previewHour: 9
"@

# Split newYamlDocs into documents
$newMatches = $regex.Matches($newYamlDocs)
for ($i = 0; $i -lt $newMatches.Count; $i++) {
    $m = $newMatches[$i]
    $type = $m.Groups[1].Value
    $id = $m.Groups[2].Value
    $startIdx = $m.Index
    $endIdx = if ($i + 1 -lt $newMatches.Count) { $newMatches[$i + 1].Index } else { $newYamlDocs.Length }
    $body = $newYamlDocs.Substring($startIdx, $endIdx - $startIdx)
    $docs[$id] = $body
    $docTypes[$id] = $type
    $docOrder.Add($id)
}

# Reassemble file in order
$sb = [System.Text.StringBuilder]::new()
foreach ($id in $docOrder) {
    if ($docs.ContainsKey($id)) {
        $sb.Append($docs[$id])
    }
}

[System.IO.File]::WriteAllText($operatingPath, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "OperatingPanel.prefab updated successfully with 32 hierarchy objects, TimeOfDayPixelStage, and TimeOfDayUIController!"

#!/usr/bin/env python3
"""TJ Racing 3D — generate Unity .meta files + the two minimal .unity scenes.

Why this exists: the project is authored as TEXT (no Unity Editor on this VM).
Every file under Assets/ needs a .meta with a stable GUID, and the two scenes
reference the C# scripts by GUID. GUIDs are deterministic: md5(relative path),
so re-running this script never churns them.

Scenes are intentionally MINIMAL: Bootstrap (TJRacingBootstrap) + Main Camera +
Directional Light. Everything else (track, cars, AI, HUD, EventSystem) is built
at runtime by TJRacingBootstrap / TrackBuilder / RaceHUD — see README.md.
The Editor normalises these files on first open (it rewrites formatting); that
is expected and harmless.

Usage: python3 tools/generate_meta_and_scenes.py   (run from the repo root)
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")


def guid_for(rel_path: str) -> str:
    return hashlib.md5(rel_path.replace(os.sep, "/").encode("utf-8")).hexdigest()


SCRIPT_META = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASMDEF_META = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_META = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

JSON_META = TEXT_META

SCENE_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path: str, rel: str, template: str) -> None:
    meta_path = path + ".meta"
    guid = guid_for(rel)
    content = template.format(guid=guid)
    if os.path.exists(meta_path):
        with open(meta_path, encoding="utf-8") as f:
            if guid in f.read():
                return  # already correct
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(content)


def generate_metas() -> dict:
    """Return {relative path: guid} for everything under Assets/."""
    guids = {}
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames.sort()
        for d in sorted(dirnames):
            full = os.path.join(dirpath, d)
            rel = os.path.relpath(full, ROOT).replace(os.sep, "/")
            guids[rel] = guid_for(rel)
            write_meta(full, rel, FOLDER_META)
        for fn in sorted(filenames):
            if fn.endswith(".meta"):
                continue
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, ROOT).replace(os.sep, "/")
            guids[rel] = guid_for(rel)
            if fn.endswith(".cs"):
                write_meta(full, rel, SCRIPT_META)
            elif fn.endswith(".asmdef"):
                write_meta(full, rel, ASMDEF_META)
            elif fn.endswith(".unity"):
                write_meta(full, rel, SCENE_META)
            elif fn.endswith(".json"):
                write_meta(full, rel, JSON_META)
            else:
                write_meta(full, rel, TEXT_META)
    return guids


SCENE_HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 1
  m_FogColor: {r: 0.05, g: 0.04, b: 0.12, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.012
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.1, g: 0.1, b: 0.22, a: 1}
  m_AmbientEquatorColor: {r: 0.07, g: 0.07, b: 0.16, a: 1}
  m_AmbientGroundColor: {r: 0.03, g: 0.03, b: 0.06, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_EnableFogOnTransparent: 1
  m_FogStripping: 0
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 300001}
  m_IndirectSpecularColor: {r: 0, g: 0, b: 0, a: 1}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMISBucketCount: 16
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousRadiusDirect: 1
    m_PVRFilteringAtrousRadiusIndirect: 1
    m_PVRFilteringAtrousRadiusAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCount: 10
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    accuratePlacement: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
"""

GAMEOBJECT = """--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{components}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""

TRANSFORM = """--- !u!4 &{id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_LocalRotation: {{x: {rx}, y: {ry}, z: {rz}, w: {rw}}}
  m_LocalPosition: {{x: {px}, y: {py}, z: {pz}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: {order}
  m_LocalEulerAnglesHint: {{x: {ex}, y: {ey}, z: {ez}}}
"""

CAMERA = """--- !u!20 &{id}
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 1
  m_BackGroundColor: {{r: 0.05, g: 0.05, b: 0.16, a: 1}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_SensorSize: {{x: 36, y: 24}}
  m_LensShift: {{x: 0, y: 0}}
  m_FocalLength: 50
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
  m_StereoMirrorMode: 0
"""

LIGHT = """--- !u!108 &{id}
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Shape: 0
  m_Color: {{r: 0.55, g: 0.6, b: 0.9, a: 1}}
  m_Intensity: 0.55
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_Cookie: {{fileID: 0}}
  m_Shadows:
    m_Type: 0
    m_Resolution: -1
    m_CustomResolution: 512
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_CookieSize: 10
  m_FalloffTable: {{fileID: 0}}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_AreaSize: {{x: 1, y: 1}}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCulling: 1
  m_ShadowRadius: 0
  m_ShadowAngle: 0
"""

AUDIO_LISTENER = """--- !u!82 &{id}
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  serializedVersion: 2
"""

MONOBEHAVIOUR = """--- !u!114 &{id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
{fields}"""


def gameobject_block(go_id, name, component_ids, order=0):
    comps = "".join("  - component: {fileID: %d}\n" % c for c in component_ids)
    return GAMEOBJECT.format(go_id=go_id, components=comps, name=name)


def build_scene(guids, bootstrap_guid, cam_pos, cam_rot_euler):
    """Bootstrap + Main Camera + Directional Light. cam_rot_euler in degrees."""
    import math

    def euler_to_quat(x, y, z):
        xr, yr, zr = math.radians(x), math.radians(y), math.radians(z)
        cx, sx = math.cos(xr / 2), math.sin(xr / 2)
        cy, sy = math.cos(yr / 2), math.sin(yr / 2)
        cz, sz = math.cos(zr / 2), math.sin(zr / 2)
        w = cx * cy * cz + sx * sy * sz
        qx = sx * cy * cz - cx * sy * sz
        qy = cx * sy * cz + sx * cy * sz
        qz = cx * cy * sz - sx * sy * cz
        return qx, qy, qz, w

    parts = [SCENE_HEADER]
    # Bootstrap GameObject (100000) + Transform (100001) + MonoBehaviour (100002)
    parts.append(gameobject_block(100000, "Bootstrap", [100001, 100002]))
    parts.append(TRANSFORM.format(id=100001, go_id=100000, rx=0, ry=0, rz=0, rw=1,
                                  px=0, py=0, pz=0, order=0, ex=0, ey=0, ez=0))
    fields = "  mainCamera: {fileID: 200001}\n  sunLight: {fileID: 300001}\n"
    parts.append(MONOBEHAVIOUR.format(id=100002, go_id=100000,
                                      script_guid=bootstrap_guid, fields=fields))
    # Main Camera (200000) + Transform (200002) + Camera (200001) + AudioListener (200003)
    parts.append(gameobject_block(200000, "Main Camera", [200002, 200001, 200003], order=1))
    qx, qy, qz, qw = euler_to_quat(*cam_rot_euler)
    parts.append(TRANSFORM.format(id=200002, go_id=200000, rx=qx, ry=qy, rz=qz, rw=qw,
                                  px=cam_pos[0], py=cam_pos[1], pz=cam_pos[2],
                                  order=1, ex=cam_rot_euler[0], ey=cam_rot_euler[1], ez=cam_rot_euler[2]))
    parts.append(CAMERA.format(id=200001, go_id=200000))
    parts.append(AUDIO_LISTENER.format(id=200003, go_id=200000))
    # Directional Light (300000) + Transform (300002) + Light (300001)
    parts.append(gameobject_block(300000, "Directional Light", [300002, 300001], order=2))
    qx, qy, qz, qw = euler_to_quat(50, -30, 0)
    parts.append(TRANSFORM.format(id=300002, go_id=300000, rx=qx, ry=qy, rz=qz, rw=qw,
                                  px=0, py=3, pz=0, order=2, ex=50, ey=-30, ez=0))
    parts.append(LIGHT.format(id=300001, go_id=300000))
    return "".join(parts)


def main():
    guids = generate_metas()
    bootstrap_rel = "Assets/Scripts/TJRacing/Core/TJRacingBootstrap.cs"
    bootstrap_guid = guids[bootstrap_rel]

    scenes_dir = os.path.join(ASSETS, "Scenes")
    os.makedirs(scenes_dir, exist_ok=True)

    menu = build_scene(guids, bootstrap_guid, (0, 2.6, -8.5), (8, 0, 0))
    race = build_scene(guids, bootstrap_guid, (0, 4.2, -12), (12, 0, 0))
    for name, content in (("MainMenu.unity", menu), ("Race_NightCity.unity", race)):
        path = os.path.join(scenes_dir, name)
        with open(path, "w", encoding="utf-8") as f:
            f.write(content)
        rel = "Assets/Scenes/" + name
        guids[rel] = guid_for(rel)
        write_meta(path, rel, SCENE_META)

    print("meta files + scenes generated.")
    print("bootstrap guid:", bootstrap_guid)
    print("files under Assets:", sum(1 for _, _, fs in os.walk(ASSETS) for _ in fs))


if __name__ == "__main__":
    sys.exit(main())

import os
import shutil

scene_path = r'Assets\Scenes\SampleScene.unity'
backup_path = r'Assets\Scenes\SampleScene.unity.bak'
shutil.copyfile(scene_path, backup_path)
print('Backed up SampleScene.unity')

def get_guid(meta_path):
    with open(meta_path, 'r', encoding='utf-8') as f:
        for line in f:
            if line.startswith('guid:'):
                return line.split(':')[1].strip()
    return None

lour_dir = r'Assets\Art\Sprites\Boss\Lour'
def sp(name):
    g = get_guid(os.path.join(lour_dir, name + '.png.meta'))
    return f'{{fileID: 21300000, guid: {g}, type: 3}}'

idle_frames = '\n'.join([f'  - {sp(f"Lour_Idle_{i}")}' for i in range(6)])
walk_frames = '\n'.join([f'  - {sp(f"Lour_Walk_{i}")}' for i in range(6)])
run_frames = '\n'.join([f'  - {sp(f"Lour_Run_{i}")}' for i in range(4)])
dash_frames = '\n'.join([f'  - {sp(f"Lour_Dash_{i}")}' for i in range(5)])
jump_frames = '\n'.join([f'  - {sp(f"Lour_Jump_{i}")}' for i in range(5)])
fire_frames = '\n'.join([f'  - {sp(f"Lour_Fire_{i}")}' for i in range(4)])
throw_frames = '\n'.join([f'  - {sp(f"Lour_Throw_{i}")}' for i in range(4)])
grenade_sprite = sp('Lour_Grenade')

ai_guid = get_guid('Assets/Scripts/Mechanics/Boss/LourBossAI.cs.meta')
health_guid = get_guid('Assets/Scripts/Mechanics/Health.cs.meta')
cartridge_guid = get_guid('Assets/Audio/cartridge_insert.wav.meta')
blast_guid = get_guid('Assets/Audio/steam_blast.wav.meta')
hiss_guid = get_guid('Assets/Audio/pneumatic_hiss.wav.meta')
hurt_guid = get_guid('Assets/Audio/Hurt.wav.meta')
death_guid = get_guid('Assets/Audio/Death.wav.meta')

lour_scene_yaml = f'''--- !u!1 &889900001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 889900002}}
  - component: {{fileID: 889900003}}
  - component: {{fileID: 889900004}}
  - component: {{fileID: 889900005}}
  - component: {{fileID: 889900006}}
  - component: {{fileID: 889900007}}
  - component: {{fileID: 889900008}}
  m_Layer: 13
  m_Name: LourBoss
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &889900002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 22, y: 43.6, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1039012175}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!212 &889900003
SpriteRenderer:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 15
  m_Sprite: {sp('Lour_Idle_0')}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {{x: 1, y: 1}}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 1
--- !u!50 &889900004
Rigidbody2D:
  serializedVersion: 4
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  m_BodyType: 0
  m_Simulated: 1
  m_UseFullKinematicContacts: 0
  m_UseAutoMass: 0
  m_Mass: 2
  m_LinearDrag: 0
  m_AngularDrag: 0.05
  m_GravityScale: 2.5
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_Interpolate: 0
  m_SleepingMode: 1
  m_CollisionDetection: 1
  m_Constraints: 4
--- !u!70 &889900005
CapsuleCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  m_Enabled: 1
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_ForceSendLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ForceReceiveLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ContactCaptureLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_CallbackLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_IsTrigger: 0
  m_UsedByEffector: 0
  m_CompositeOperation: 0
  m_CompositeOrder: 0
  m_Offset: {{x: 0, y: 1.0}}
  m_Size: {{x: 0.85, y: 1.9}}
  m_Direction: 0
--- !u!82 &889900006
AudioSource:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  m_Enabled: 1
  serializedVersion: 4
  OutputAudioMixerGroup: {{fileID: 0}}
  m_audioClip: {{fileID: 0}}
  m_PlayOnAwake: 0
  m_Volume: 1
  m_Pitch: 1
  Loop: 0
  Mute: 0
  Spatialize: 0
  SpatializePostEffects: 0
  Priority: 128
  DopplerLevel: 1
  MinDistance: 1
  MaxDistance: 50
  Pan2D: 0
  rolloffMode: 0
  BypassEffects: 0
  BypassListenerEffects: 0
  BypassReverbZones: 0
  rolloffCustomCurve:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 0
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334
    - serializedVersion: 3
      time: 1
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  panLevelCustomCurve:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 0
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  spreadCustomCurve:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 0
      value: 0
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
  reverbZoneMixCustomCurve:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
      time: 0
      value: 1
      inSlope: 0
      outSlope: 0
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
--- !u!114 &889900007
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {health_guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  maxHP: 25
--- !u!114 &889900008
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 889900001}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ai_guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  path: {{fileID: 0}}
  ouch: {{fileID: 8300000, guid: {hurt_guid}, type: 3}}
  currentState: 0
  isPhase2: 0
  isFacingLeft: 1
  idleFrames:
{idle_frames}
  walkFrames:
{walk_frames}
  runFrames:
{run_frames}
  dashFrames:
{dash_frames}
  jumpFrames:
{jump_frames}
  fireFrames:
{fire_frames}
  throwFrames:
{throw_frames}
  grenadeSprite: {grenade_sprite}
  detectionRadius: 20
  minEngageDistance: 6.5
  maxEngageDistance: 12.5
  walkSpeed: 3.2
  sprintSpeed: 6.5
  dashSpeed: 15
  dashDuration: 0.35
  aimDurationPhase1: 0.85
  aimDurationPhase2: 0.5
  sniperBulletSpeed: 30
  sniperDamage: 1
  sniperCooldown: 2.8
  muzzlePoint: {{fileID: 0}}
  grenadeCooldown: 4.2
  grenadeThrowSpeed: 8.5
  grenadeClusterCount: 5
  shootSound: {{fileID: 8300000, guid: {blast_guid}, type: 3}}
  aimBeepSound: {{fileID: 8300000, guid: {cartridge_guid}, type: 3}}
  grenadeThrowSound: {{fileID: 8300000, guid: {blast_guid}, type: 3}}
  dashSound: {{fileID: 8300000, guid: {hiss_guid}, type: 3}}
  hurtSound: {{fileID: 8300000, guid: {hurt_guid}, type: 3}}
  retreatSound: {{fileID: 8300000, guid: {death_guid}, type: 3}}
  aimLaser: {{fileID: 0}}
  laserAimColor: {{r: 1, g: 0.1, b: 0.1, a: 0.65}}
  laserLockColor: {{r: 1, g: 0.2, b: 0.2, a: 1}}
'''

with open(scene_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Add child to 1039012175
target_str = '''  m_Children:
  - {fileID: 868189377}
  - {fileID: 1078703004}
  - {fileID: 2039150449}
  - {fileID: 730887209}
  - {fileID: 44530666}
  - {fileID: 759164645}
  - {fileID: 1021498524}
  - {fileID: 1900797854}'''

replacement_str = '''  m_Children:
  - {fileID: 868189377}
  - {fileID: 1078703004}
  - {fileID: 2039150449}
  - {fileID: 730887209}
  - {fileID: 44530666}
  - {fileID: 759164645}
  - {fileID: 1021498524}
  - {fileID: 1900797854}
  - {fileID: 889900002}'''

if target_str in content:
    content = content.replace(target_str, replacement_str, 1)
    content += '\n' + lour_scene_yaml
    with open(scene_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print('Successfully added LourBoss to SampleScene under XBand_Container_Deck!')
else:
    print('Could not find target m_Children block in SampleScene!')

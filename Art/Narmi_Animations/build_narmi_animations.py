"""Run inside the connected Blender scene. Adds only Narmi's rig and two clips."""
import bpy
import math
import json
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(r'C:\Users\lemon\Desktop\PPS\Art\Narmi_Animations')
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
root = bpy.data.objects['나르미']
collection = bpy.data.collections['나르미 · Narmi']
parts = [o for o in collection.objects if o.type in {'MESH', 'CURVE'}]
assert not bpy.data.objects.get('Narmi · Animation Rig'), 'Rig already exists'
assert not any(o.animation_data for o in parts), 'Preserve existing animation'
before = {o.name: o.matrix_world.copy() for o in parts}
dari_before = {o.name: [list(row) for row in o.matrix_world] for o in bpy.data.collections['달이 · Dari'].objects}
source_path = bpy.data.filepath

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
arm = bpy.data.armatures.new('Narmi · Animation Skeleton')
rig = bpy.data.objects.new('Narmi · Animation Rig', arm)
collection.objects.link(rig)
rig.parent = root
rig.matrix_parent_inverse = root.matrix_world.inverted()
rig.matrix_world = Matrix.Identity(4)
rig.show_in_front = True
arm.display_type = 'STICK'
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
cx = root.matrix_world.translation.x
spec = [
    ('Root', (cx, 0, 0), None),
    ('Body', (cx, .08, .85), 'Root'),
    ('Head', (cx, .0, 1.54), 'Body'),
    ('Wing.L', (cx+.57, .06, 1.36), 'Body'),
    ('Wing.R', (cx-.57, .06, 1.36), 'Body'),
    ('Foot.L', (cx+.296, .0, .40), 'Root'),
    ('Foot.R', (cx-.296, .0, .40), 'Root'),
    ('Tail', (cx+.42, .66, .47), 'Body'),
    ('Scarf.Upper', (cx+.70, .22, 1.50), 'Body'),
    ('Scarf.Lower', (cx+.70, .02, 1.45), 'Body'),
    ('Eye.L', (cx-.6575, -1.388477, 2.645), 'Head'),
    ('Eye.R', (cx+.6625, -1.388478, 2.645), 'Head'),
]
for name, head, parent in spec:
    bone = arm.edit_bones.new(name)
    bone.head = head
    bone.tail = Vector(head) + Vector((0, .32, 0))
    if parent:
        bone.parent = arm.edit_bones[parent]
    bone.use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()

def attachment(name):
    if 'three-toed -1' in name: return 'Foot.R'
    if 'three-toed 1' in name: return 'Foot.L'
    if 'left flipper' in name: return 'Wing.L'
    if 'right flipper' in name: return 'Wing.R'
    if 'upper scarf tail' in name: return 'Scarf.Upper'
    if 'lower scarf tail' in name: return 'Scarf.Lower'
    if 'tail feathers' in name or 'white tail base' in name: return 'Tail'
    if 'left glossy eye' in name: return 'Eye.L'
    if 'right glossy eye' in name: return 'Eye.R'
    if 'eye reflection' in name: return 'Eye.R' if name.endswith('.001') else 'Eye.L'
    if 'round white body' in name or 'scarf collar' in name or 'scarf knot' in name: return 'Body'
    return 'Head'

for obj in parts:
    obj['narmi_original_parent'] = root.name
    obj['narmi_original_world_matrix'] = [v for row in before[obj.name] for v in row]
    basis = obj.matrix_basis.copy()
    obj.parent = rig
    obj.parent_type = 'BONE'
    obj.parent_bone = attachment(obj.name)
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    parent_matrix = obj.matrix_world.copy()
    obj.matrix_parent_inverse = parent_matrix.inverted() @ before[obj.name] @ basis.inverted()
    obj.matrix_basis = basis
    bpy.context.view_layer.update()
bind_error = max(abs(obj.matrix_world[i][j]-before[obj.name][i][j]) for obj in parts for i in range(4) for j in range(4))
assert bind_error < 0.0001, f'Bind moved mesh: {bind_error}'
for pb in rig.pose.bones:
    pb.rotation_mode = 'XYZ'

feet = {}
for name in ['Foot.L', 'Foot.R']:
    obj = next(o for o in parts if o.parent_bone == name)
    verts = np.empty(len(obj.data.vertices)*3, dtype=np.float64)
    obj.data.vertices.foreach_get('co', verts)
    feet[name] = (obj, verts.reshape((-1,3)))

def ground_foot(bone_name, clearance):
    """Use real geometry to plant the sole; do not rely on bounding boxes."""
    bpy.context.view_layer.update()
    obj, verts = feet[bone_name]
    mat = np.array(obj.matrix_world, dtype=np.float64)
    lowest = float((verts @ mat[2,:3] + mat[2,3]).min())
    rig.pose.bones[bone_name].location.z += clearance-lowest

def set_pose(name, loc=(0,0,0), rot=(0,0,0), scale=(1,1,1)):
    pb = rig.pose.bones[name]
    pb.location = loc
    pb.rotation_euler = tuple(math.radians(a) for a in rot)
    pb.scale = scale

def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves

def animate(kind, period):
    ad = rig.animation_data_create()
    action = bpy.data.actions.new('Narmi_' + kind)
    ad.action = action
    action.use_fake_user = True
    action['character'] = '나르미 / Narmi'
    action['loop_frames'] = period
    action['fps'] = 24
    action['in_place'] = True
    action['notes'] = 'Endpoint repeats frame 1; play 1 through %d inclusive.' % period
    for f in range(1, period+2):
        scene.frame_set(f)
        t = (f-1)/period
        p = 2*math.pi*t
        for pb in rig.pose.bones:
            set_pose(pb.name)
        if kind == 'Idle':
            breath = .5-.5*math.cos(p)
            set_pose('Body', loc=(.014*math.sin(p),0,.020*breath),
                     rot=(.45*math.sin(p), .65*math.sin(p), .35*math.sin(p)),
                     scale=(1+.008*breath,1+.010*breath,1+.014*breath))
            set_pose('Head', rot=(-.75*math.sin(p),-.45*math.sin(p),1.1*math.sin(p)))
            for side, sign in [('L',1), ('R',-1)]:
                set_pose('Wing.'+side, rot=(1.0*math.sin(p-.3),sign*(1.4+1.4*math.sin(p)),0))
            set_pose('Tail', rot=(1.5*math.sin(p-.6),0,1.7*math.sin(p-.4)))
            set_pose('Scarf.Upper', rot=(1.5*math.sin(p-.6),2*math.sin(p-.5),1*math.sin(p-.6)))
            set_pose('Scarf.Lower', rot=(2*math.sin(p-.9),2.5*math.sin(p-.8),1.5*math.sin(p-.9)))
            blink = {40:1,41:.72,42:.20,43:.065,44:.18,45:.70,46:1}.get(f,1)
            for eye in ['Eye.L', 'Eye.R']:
                set_pose(eye, scale=(1,1,blink))
            for foot in feet:
                ground_foot(foot,.006)
        else:
            # Compact running bird: planted stance, lifted recovery and delayed accessories.
            bounce = .5-.5*math.cos(2*p)
            set_pose('Body', loc=(.040*math.cos(p),-.10,.025+.115*bounce),
                     rot=(7.0+1.2*math.sin(2*p),2.4*math.cos(p),2.3*math.cos(p)),
                     scale=(1+.012*math.cos(2*p),1+.010*math.cos(2*p),1-.018*math.cos(2*p)))
            set_pose('Head', loc=(0,0,.012*math.sin(2*p-.4)),
                     rot=(-3.5-.8*math.sin(2*p-.4),-1.5*math.cos(p-.2),-1.3*math.cos(p-.2)))
            for side, offset, sign in [('L',0,1),('R',.5,-1)]:
                u = (t+offset)%1
                q = 2*math.pi*u
                if u < .5:
                    y = -.46+1.84*u
                    lift = 0
                    pitch = 20*max(0,(u-.32)/.18)
                else:
                    v=(u-.5)*2
                    y=.46*math.cos(math.pi*v)
                    lift=.36*(math.sin(math.pi*v)**1.1)
                    pitch=20*math.cos(math.pi*v)-23*math.sin(math.pi*v)
                set_pose('Foot.'+side, loc=(sign*.025*math.sin(q),y,0),rot=(pitch,0,sign*3*math.sin(q)))
                ground_foot('Foot.'+side,.014+lift)
                set_pose('Wing.'+side, rot=(-19*math.sin(q-.25),sign*(9+3.5*math.sin(2*p-.6)),sign*3*math.cos(q)))
            set_pose('Tail',rot=(-6+4*math.sin(2*p-.8),2*math.sin(p-.5),6*math.sin(p-.65)))
            set_pose('Scarf.Upper',rot=(9+7*math.sin(2*p-.8),-10+7*math.sin(2*p-.6),18+5*math.sin(p-.5)))
            set_pose('Scarf.Lower',rot=(-7+10*math.sin(2*p-1.2),-12+9*math.sin(2*p-1.0),14+7*math.sin(p-.8)))
        for pb in rig.pose.bones:
            for prop in ['location','rotation_euler','scale']:
                pb.keyframe_insert(data_path=prop, frame=f, group=pb.name)
    for fc in curves(action):
        for key in fc.keyframe_points:
            key.interpolation = 'BEZIER'
            key.handle_left_type = 'AUTO_CLAMPED'
            key.handle_right_type = 'AUTO_CLAMPED'
        fc.modifiers.new('CYCLES')
    action.use_frame_range = True
    action.frame_start = 1
    action.frame_end = period+1
    ad.action = None
    return action

idle = animate('Idle',72)
run = animate('Run',20)
ad = rig.animation_data
track = ad.nla_tracks.new()
track.name = 'Narmi · Idle then Run · preview'
strip = track.strips.new('Idle · breathing / blink · 3 sec',1,idle)
strip.action_frame_start=1
strip.action_frame_end=73
strip.extrapolation='NOTHING'
strip = track.strips.new('Run · in place · 6 loops',73,run)
strip.action_frame_start=1
strip.action_frame_end=21
strip.repeat=6
strip.extrapolation='NOTHING'
scene.render.fps = 24
scene.frame_start=1
scene.frame_end=192
scene.timeline_markers.new('NARMI · IDLE (1–72)', frame=1)
scene.timeline_markers.new('NARMI · RUN (73–192)', frame=73)
rig['animation_help'] = 'Two looping Actions: Narmi_Idle (1–72), Narmi_Run (1–20), 24 fps. NLA timeline previews Idle then six Run cycles. Root stays in place.'
rig['source_blend'] = source_path
rig['rig_type'] = 'Rigid bone parenting preserves existing mesh, curves, materials and silhouette.'
rig['rest_bind_max_matrix_error'] = bind_error
scene.frame_set(1)
bpy.context.view_layer.update()
assert all(dari_before[o.name] == [list(row) for row in o.matrix_world] for o in bpy.data.collections['달이 · Dari'].objects)

# A dedicated review scene shares the original Narmi objects and animation.
preview = bpy.data.scenes.new('Narmi · Animation Preview')
preview.collection.children.link(collection)
preview.render.engine='BLENDER_EEVEE'
preview.render.resolution_x=640
preview.render.resolution_y=640
preview.render.resolution_percentage=100
preview.render.image_settings.file_format='PNG'
preview.render.fps=24
preview.frame_start=1
preview.frame_end=192
preview.world=bpy.data.worlds.new('Narmi · preview studio world')
preview.world.use_nodes=True
preview.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.32,.38,.48,1)
preview.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6
preview.view_settings.view_transform='AgX'
studio=bpy.data.collections.new('Narmi · Animation Preview Studio')
preview.collection.children.link(studio)
cam_data=bpy.data.cameras.new('Narmi · animation review camera')
cam=bpy.data.objects.new(cam_data.name,cam_data)
studio.objects.link(cam)
cam.location=(cx+6,-16,6.6)
target=Vector((cx,-.1,2.5))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'
cam.data.ortho_scale=6.45
preview.camera=cam
for name,loc,power,size in [
    ('Key',(cx-4,-6,8),1400,5),
    ('Fill',(cx+5,-4,5),1000,4),
    ('Rim',(cx+1,4,7),1800,3)]:
    data=bpy.data.lights.new('Narmi preview · '+name,'AREA')
    data.energy=power
    data.shape='DISK'
    data.size=size
    light=bpy.data.objects.new(data.name,data)
    studio.objects.link(light)
    light.location=loc
    light.rotation_euler=(Vector((cx,0,2.1))-light.location).to_track_quat('-Z','Y').to_euler()
mesh=bpy.data.meshes.new('Narmi preview · ground mesh')
mesh.from_pydata([(-200,-200,-.001),(200,-200,-.001),(200,200,-.001),(-200,200,-.001)],[],[(0,1,2,3)])
plane=bpy.data.objects.new('Narmi preview · ground',mesh)
studio.objects.link(plane)
mat=bpy.data.materials.new('Narmi preview · matte blue grey')
mat.diffuse_color=(.14,.19,.25,1)
mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=mat.diffuse_color
mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.9
mesh.materials.append(mat)
preview.timeline_markers.new('IDLE · breathing + blink',frame=1)
preview.timeline_markers.new('RUN · in place loop',frame=73)
preview.frame_set(1)
notes=bpy.data.texts.new('Narmi · Animation README')
notes.write('나르미 Idle / Run\n\n24 fps. Two reusable looping Actions on Narmi · Animation Rig.\nNarmi_Idle: frames 1–72; closing key at 73. Gentle breathing, wing settling, head sway, one blink.\nNarmi_Run: frames 1–20; closing key at 21. In-place, alternating planted feet, bounce, wing swing, scarf/tail follow-through.\nNLA preview timeline: Idle 1–72, Run 73–192. Playback with Space.\nTo edit a clip: mute the NLA track and select its Action in the Action Editor.\nMeshes and materials retained. Rigid bone parenting; no remeshing. Dari was left untouched.\nOriginal file: '+source_path+'\n')
result={'rig':rig.name,'actions':[idle.name,run.name],'bind_error':bind_error,'dari_unchanged':True,'preview_scene':preview.name,'output_dir':str(OUT)}

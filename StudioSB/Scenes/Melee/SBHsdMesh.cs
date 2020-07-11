﻿using System;
using System.Collections.Generic;
using HSDRaw.Common;
using HSDRaw.Tools;
using HSDRaw.GX;
using SFGenericModel;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using SFGraphics.GLObjects.Shaders;
using IONET.Core.Model;

namespace StudioSB.Scenes.Melee
{
    public class SBHsdRenderMesh : GenericMesh<SBHsdVertex>
    {
        public SBHsdRenderMesh(List<SBHsdVertex> vertices, PrimitiveType type) : base(vertices, type)
        {

        }
    }

    public class SBHsdMesh : ISBMesh
    {
        public HSD_DOBJ DOBJ{ get => _dobj; }
        private HSD_DOBJ _dobj { get; set; }

        public override int PolyCount => base.PolyCount;
        public override int VertexCount => base.VertexCount;

        private List<SBHsdRenderMesh> renderMesh = new List<SBHsdRenderMesh>();
        
        private SBHsdMaterial _material { get => Material as SBHsdMaterial; set => Material = value; }

        //TODO: update dobj when parent string name changes

        public override string ToString()
        {
            return Name;
        }

        public SBHsdMesh(HSD_DOBJ dobj, SBBone parent)
        {
            Name = "DOBJ";

            _dobj = dobj;

            _material = new SBHsdMaterial(dobj);

            ParentBone = parent.Name;

            Visible = true;
        }

        public void Refresh(HSDScene scene)
        {
            RefreshRendering(scene.Skeleton);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="skeleton"></param>
        private void RefreshRendering(ISBSkeleton skeleton)
        {
            BoundingSphere = new Rendering.Bounding.BoundingSphere(0, 0, 0, 0);
            renderMesh.Clear();

            List<SBHsdBone> bones = new List<SBHsdBone>();
            foreach (SBHsdBone bone in skeleton.Bones)
                bones.Add(bone);

            GX_VertexAccessor accessor = new GX_VertexAccessor();

            List<GX_Vertex> allVerts = new List<GX_Vertex>();

            if (_dobj.Pobj != null)
                foreach (var pobj in _dobj.Pobj.List)
                {
                    //foreach (var va in pobj.Attributes)
                    //    if(va.AttributeName != GXAttribName.GX_VA_POS && va.AttributeName != GXAttribName.GX_VA_NRM
                    //        && va.AttributeName != GXAttribName.GX_VA_NULL && va.AttributeName != GXAttribName.GX_VA_PNMTXIDX
                    //        && va.AttributeName != GXAttribName.GX_VA_TEX0)
                    //    Console.WriteLine($"{Name} {va.AttributeName} {va.AttributeType} {va.CompCount} {va.CompType} {va.Scale} {va.Stride}");
                    var dl = pobj.ToDisplayList();
                    var vertices = GX_VertexAccessor.GetDecodedVertices(dl, pobj);
                    allVerts.AddRange(vertices);

                    var offset = 0;
                    foreach (var v in dl.Primitives)
                    {
                        List<GX_Vertex> strip = new List<GX_Vertex>();
                        for (int i = 0; i < v.Count; i++)
                            strip.Add(vertices[offset + i]);
                        offset += v.Count;

                        var rm = new SBHsdRenderMesh(GXVertexToHsdVertex(strip, bones, pobj.EnvelopeWeights == null ? null : pobj.EnvelopeWeights), GXtoGL.GLPrimitiveType(v.PrimitiveType));
                        
                        renderMesh.Add(rm);
                    }

                }

            GenerateBoundingSphere(allVerts);
        }

        private void GenerateBoundingSphere(List<GX_Vertex> vertices)
        {
            List<Vector3> positions = new List<Vector3>();
            foreach (var v in vertices)
                positions.Add(GXtoGL.GLVector3(v.POS));
            BoundingSphere = new Rendering.Bounding.BoundingSphere(positions);
        }

        /// <summary>
        /// 
        /// </summary>
        public void ImportPOBJs(IOMesh mesh, SBSkeleton skeleton, POBJ_Generator compressor, GXAttribName[] attrGroup, bool singleBind = false)
        {
            DOBJ.Pobj = null;
            
            // get gx vertices and rigging groups
            List<HSD_Envelope> weightList = null;
            var verts = IOVertexToGXVertex(mesh.Vertices, skeleton, out weightList, Matrix4.Identity);

            // reorder to triangle strips
            List<GX_Vertex> triStrip = new List<GX_Vertex>();
            foreach(var p in mesh.Polygons)
                foreach (var i in p.Indicies)
                    triStrip.Add(verts[(int)i]);
            
            // compress and generate display lists
            DOBJ.Pobj = compressor.CreatePOBJsFromTriangleList(triStrip, attrGroup, weightList);

            if (singleBind || ParentBone != "JOBJ_0")
            {
                foreach(var v in DOBJ.Pobj.List)
                {
                    v.EnvelopeWeights = null;
                    DOBJ.Pobj.Flags = 0;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ioverts"></param>
        /// <param name="skeleton"></param>
        /// <param name="weightList"></param>
        /// <returns></returns>
        private List<GX_Vertex> IOVertexToGXVertex(List<IOVertex> ioverts, SBSkeleton skeleton, out List<HSD_Envelope> weightList, Matrix4 transform)
        {
            weightList = new List<HSD_Envelope>();

            Dictionary<Tuple<Vector4, Vector4>, int> weightToWeightListIndex = new Dictionary<Tuple<Vector4, Vector4>, int>();

            List<GX_Vertex> gxverts = new List<GX_Vertex>();
            foreach(var iovert in ioverts)
            {
                // load bone weights
                Vector4 boneIndices = Vector4.Zero;
                Vector4 boneWeights = Vector4.Zero;

                int bi = 0;
                foreach(var bw in iovert.Envelope.Weights)
                {
                    boneIndices[bi] = skeleton.IndexOfBone(skeleton[bw.BoneName]);
                    boneWeights[bi] = bw.Weight;
                    bi++;
                }

                // get bone weight mapping
                var tuple = new Tuple<Vector4, Vector4>(boneIndices, boneWeights);

                if (!weightToWeightListIndex.ContainsKey(tuple))
                {
                    HSD_Envelope weight = new HSD_Envelope();
                    for (int i = 0; i < 4; i++)
                    {
                        if (boneWeights[i] != 0)
                        {
                            var jobj = ((SBHsdBone)skeleton.Bones[(int)boneIndices[i]]).GetJOBJ();
                            if (jobj == null)
                                throw new Exception("Error getting JOBJ for rigging");
                            weight.Add(jobj, boneWeights[i]);
                        }
                    }
                    weightToWeightListIndex.Add(tuple, weightList.Count);
                    weightList.Add(weight);
                }

                int index = weightToWeightListIndex[tuple];

                if (index * 3 > ushort.MaxValue)
                    SBConsole.WriteLine("Warning!: To many weights for one polygon object, try splitting the polygons to more DOBJs");

                // load attributes
                GX_Vertex gxvert = new GX_Vertex();
                gxvert.PNMTXIDX = (ushort)(index * 3);

                var p = Vector3.TransformPosition(new Vector3(iovert.Position.X, iovert.Position.Y, iovert.Position.Z), transform);
                var n = Vector3.TransformNormal(new Vector3(iovert.Normal.X, iovert.Normal.Y, iovert.Normal.Z), transform);

                gxvert.POS = new GXVector3(p.X, p.Y, p.Z);
                gxvert.NRM = new GXVector3(n.X, n.Y, n.Z);

                if(iovert.UVs.Count > 0)
                    gxvert.TEX0 = new GXVector2(iovert.UVs[0].X, iovert.UVs[0].Y);

                if (iovert.Colors.Count > 0)
                    gxvert.CLR0 = new GXColor4(iovert.Colors[0].X, iovert.Colors[0].Y, iovert.Colors[0].Z, iovert.Colors[0].W);

                gxverts.Add(gxvert);
            }

            return gxverts;
        }

        /// <summary>
        /// Clears the texture in the mobj
        /// </summary>
        public void ClearTextures()
        {
            DOBJ.Mobj.RenderFlags = RENDER_MODE.ALPHA_COMPAT | RENDER_MODE.DIFFUSE;
            DOBJ.Mobj.Textures = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="shader"></param>
        public void Draw(HSDScene scene, Shader shader)
        {
            if (!Visible)
                return;

            _material.Bind(scene, shader);

            shader.SetMatrix4x4("singleBind", scene.Skeleton[ParentBone].AnimatedWorldTransform);

            foreach (var rm in renderMesh)
            {
                rm.Draw(shader);
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="bones"></param>
        /// <param name="jobjweights"></param>
        /// <returns></returns>
        public static List<SBHsdVertex> GXVertexToHsdVertex(List<GX_Vertex> vertices, List<SBHsdBone> bones, HSD_Envelope[] jobjweights)
        {
            List<SBHsdVertex> newvertices = new List<SBHsdVertex>();

            foreach(var v in vertices)
            {
                Vector4 bone = new Vector4();
                Vector4 weights = new Vector4();

                if (jobjweights != null)
                {
                    if(v.PNMTXIDX / 3 >= jobjweights.Length)
                        Console.WriteLine(jobjweights.Length+ " " + (v.PNMTXIDX));

                    var jobjWeight = jobjweights[v.PNMTXIDX / 3];

                    for (int i = 0; i < Math.Min(4, jobjWeight.JOBJs.Length); i++)
                        bone[i] = bones.FindIndex(e => e.GetJOBJ()._s == jobjWeight.JOBJs[i]._s);

                    for (int i = 0; i < Math.Min(4, jobjWeight.JOBJs.Length); i++)
                        weights[i] = jobjWeight.Weights[i];
                }
                
                newvertices.Add(new SBHsdVertex(v.POS, v.NRM, v.TEX0, bone, weights));

            } 

            return newvertices;
        }
        
    }
}

﻿using FSO.Common;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FSO.Files.RC
{
    public class DGRP3DGeometry
    {
        public bool Rendered = false;
        public Texture2D Pixel;
        public ushort PixelSPR;
        public ushort PixelDir;

        public ushort CustomTexture;
        public static Func<string, Texture2D> ReplTextureProvider;

        public List<VertexPositionTexture> SVerts; //simplified vertices
        public List<int> SIndices; //simplified indices

        public VertexBuffer Verts;
        public IndexBuffer Indices;
        public int PrimCount;

        public void SComplete(GraphicsDevice gd)
        {
            Rendered = true;
            Verts?.Dispose();
            Indices?.Dispose();

            PrimCount = SIndices.Count / 3;
            if (PrimCount > 0)
            {
                Verts = new VertexBuffer(gd, typeof(VertexPositionTexture), SVerts.Count, BufferUsage.None);
                Verts.SetData(SVerts.ToArray());
                Indices = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, SIndices.Count, BufferUsage.None);
                Indices.SetData(SIndices.ToArray());
            }

            if (!IffFile.RETAIN_CHUNK_DATA) {
                SVerts = null;
                SIndices = null;
            }
        }

        public DGRP3DGeometry() { }
        public DGRP3DGeometry(IoBuffer io, DGRP source, GraphicsDevice gd)
        {
            PixelSPR = io.ReadUInt16();
            PixelDir = io.ReadUInt16();
            if (PixelDir == 65535)
            {
                CustomTexture = 1;

                var name = source.ChunkParent.Filename.Replace('.', '_').Replace("spf", "iff");
                name += "_TEX_" + PixelSPR + ".png";
                Pixel = ReplTextureProvider(name);
                if (Pixel == null)
                {
                    Pixel = source.ChunkParent.Get<MTEX>(PixelSPR)?.GetTexture(gd);
                }
            }
            else
            {
                Pixel = source.GetImage(1, 3, PixelDir).Sprites[PixelSPR].GetTexture(gd);
            }

            var vertCount = io.ReadInt32();
            SVerts = new List<VertexPositionTexture>();
            for (int i=0; i<vertCount; i++)
            {
                var x = io.ReadFloat();
                var y = io.ReadFloat();
                var z = io.ReadFloat();
                var u = io.ReadFloat();
                var v = io.ReadFloat();
                SVerts.Add(new VertexPositionTexture(new Vector3(x, y, z), new Vector2(u, v)));
            }
            var indexCount = io.ReadInt32();
            SIndices = ToTArray<int>(io.ReadBytes(indexCount * 4)).ToList();

            SComplete(gd);
        }

        public DGRP3DGeometry(string[] splitName, OBJ obj, List<int[]> indices, DGRP source, GraphicsDevice gd)
        {
            if (splitName[1] == "SPR")
            {
                PixelSPR = ushort.Parse(splitName[3]);
                PixelDir = ushort.Parse(splitName[2].Substring(3));
                Pixel = source.GetImage(1, 3, PixelDir).Sprites[PixelSPR].GetTexture(gd);
            }
            else
            {
                PixelSPR = ushort.Parse(splitName[2]);
                CustomTexture = 1;
                PixelDir = 65535;

                var name = source.ChunkParent.Filename.Replace('.', '_').Replace("spf", "iff");
                name += "_TEX_" + PixelSPR + ".png";
                Pixel = ReplTextureProvider(name);
                if (Pixel == null)
                {
                    Pixel = source.ChunkParent.Get<MTEX>(PixelSPR)?.GetTexture(gd);
                }
            }

            SVerts = new List<VertexPositionTexture>();
            SIndices = new List<int>();
            var dict = new Dictionary<Tuple<int, int>, int>();

            foreach (var ind in indices)
            {
                var tup = new Tuple<int, int>(ind[0], ind[1]);
                int targ;
                if (!dict.TryGetValue(tup, out targ))
                {
                    //add a vertex
                    targ = SVerts.Count;
                    var vert = new VertexPositionTexture(obj.Vertices[ind[0] - 1], obj.TextureCoords[ind[1] - 1]);
                    vert.TextureCoordinate.Y = 1 - vert.TextureCoordinate.Y;
                    SVerts.Add(vert);
                    dict[tup] = targ;
                }
                SIndices.Add(targ);
            }

            SComplete(gd);
        }

        public void Save(IoWriter io)
        {
            io.WriteUInt16(PixelSPR);
            io.WriteUInt16(PixelDir);
            io.WriteInt32(SVerts.Count);
            foreach (var vert in SVerts)
            {
                io.WriteFloat(vert.Position.X);
                io.WriteFloat(vert.Position.Y);
                io.WriteFloat(vert.Position.Z);
                io.WriteFloat(vert.TextureCoordinate.X);
                io.WriteFloat(vert.TextureCoordinate.Y);
            }
            io.WriteInt32(SIndices.Count);
            io.WriteBytes(ToByteArray(SIndices.ToArray()));
        }

        private string GetOName(int dyn)
        {
            if (CustomTexture == 0)
            {
                return dyn + "_SPR_rot" + PixelDir + "_" + PixelSPR;
            } else
            {
                return dyn + "_TEX_"+PixelSPR;
            }
        }

        public void SaveOBJ(StreamWriter io, int dyn, ref int baseInd)
        {
            string o_name = GetOName(dyn);

            io.WriteLine("usemtl " + o_name);
            io.WriteLine("o "+o_name);
            foreach (var vert in SVerts)
            {
                io.WriteLine("v "+vert.Position.X.ToString(CultureInfo.InvariantCulture) +" "+vert.Position.Y.ToString(CultureInfo.InvariantCulture) +" "+vert.Position.Z.ToString(CultureInfo.InvariantCulture));
            }
            foreach (var vert in SVerts)
            {
                io.WriteLine("vt " + vert.TextureCoordinate.X.ToString(CultureInfo.InvariantCulture) + " " + (1-vert.TextureCoordinate.Y).ToString(CultureInfo.InvariantCulture));
            }
            io.Write("f ");
            var ticker = 0;
            var j = 0;
            foreach (var ind in SIndices)
            {
                var i = ind + baseInd;
                io.Write(i+"/"+i + " ");
                if (++ticker == 3)
                {
                    io.WriteLine("");
                    if (j < SIndices.Count-1) io.Write("f ");
                    ticker = 0;
                }
                j++;
            }
            baseInd += SVerts.Count;
        }

        public void SaveMTL(StreamWriter io, int dyn, string path)
        {
            var oname = GetOName(dyn);
            if (Pixel != null) {
                Common.Utils.GameThread.NextUpdate(x =>
                {
                    try
                    {
                        using (var io2 = File.Open(Path.Combine(path, oname + ".png"), FileMode.Create))
                            Pixel.SaveAsPng(io2, Pixel.Width, Pixel.Height);
                    } catch (Exception e)
                    {

                    }
                });
            }
            io.WriteLine("newmtl " + oname);
            io.WriteLine("Ka 1.000 1.000 1.000");
            io.WriteLine("Kd 1.000 1.000 1.000");
            io.WriteLine("Ks 0.000 0.000 0.000");

            io.WriteLine("Ns 10.0000");
            io.WriteLine("illum 2");

            io.WriteLine("map_Kd "+oname+".png");
            io.WriteLine("map_d "+ oname + ".png");
        }

        public void Dispose()
        {
            Verts?.Dispose();
            Indices?.Dispose();
        }

        private static T[] ToTArray<T>(byte[] input)
        {
            var result = new T[input.Length / Marshal.SizeOf(typeof(T))];
            Buffer.BlockCopy(input, 0, result, 0, input.Length);
            return result;
        }

        public static byte[] ToByteArray<T>(T[] input)
        {
            var result = new byte[input.Length * Marshal.SizeOf(typeof(T))];
            Buffer.BlockCopy(input, 0, result, 0, result.Length);
            return result;
        }
    }
}
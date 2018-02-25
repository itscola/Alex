﻿using System;
using Alex.Graphics.Models;
using Alex.Rendering;
using Alex.Utils;
using log4net;
using Microsoft.Xna.Framework;
using ResourcePackLib.Json.BlockStates;

namespace Alex.Blocks
{
    public class Block
    {
	    private static readonly ILog Log = LogManager.GetLogger(typeof(Block));
	    
		public uint BlockStateID { get; }

        public int BlockId { get; }
        public byte Metadata { get; }
        public bool Solid { get; set; }
		public bool Transparent { get; set; }
		public bool Renderable { get; set; }
		public bool HasHitbox { get; set; }
		public float Drag { get; set; }

	    public double AmbientOcclusionLightValue = 1.0;
	    public int LightValue = 0;
	    public int LightOpacity = 0;

		public Model BlockModel { get; set; }
	    protected Block(byte blockId, byte metadata) : this(GetBlockStateID(blockId, metadata))
	    {
		    
	    }

	    public Block(uint blockStateId)
	    {
		    BlockStateID = blockStateId;
		    BlockId = (int)(blockStateId >> 4);
		    Metadata = (byte)(blockStateId & 0x0F);

			Solid = true;
		    Transparent = false;
		    Renderable = true;
		    HasHitbox = true;

		    SetColor(TextureSide.All, Color.White);
		}

	    public BoundingBox GetBoundingBox(Vector3 blockPosition)
	    {
			if (BlockModel == null)
				return new BoundingBox(blockPosition, blockPosition + Vector3.One);

		    return BlockModel.GetBoundingBox(blockPosition, this);
		}

        public VertexPositionNormalTextureColor[] GetVertices(Vector3 position, World world)
        {
	        if (BlockModel == null)
				return new VertexPositionNormalTextureColor[0];

			return BlockModel.GetVertices(world, position, this);
        }

	    public void SetColor(TextureSide side, Color color)
        {
            switch (side)
            {
                case TextureSide.Top:
                    TopColor = color;
                    break;
                case TextureSide.Bottom:
                    BottomColor = color;
                    break;
                case TextureSide.Side:
                    SideColor = color;
                    break;
                case TextureSide.All:
                    TopColor = color;
                    BottomColor = color;
                    SideColor = color;
                    break;
            }
        }

        public Color TopColor { get; private set; }
        public Color SideColor { get; private set; }
		public Color BottomColor { get; private set; }

	    public string DisplayName { get; set; } = null;
	    public override string ToString()
	    {
		    return DisplayName ?? GetType().Name;
	    }

	    public static uint GetBlockStateID(int id, byte meta)
	    {
		    if (id < 0) throw new ArgumentOutOfRangeException();

		    return (uint) (id << 4 | meta);
	    }

	    public static void StateIDToRaw(uint stateId, out int id, out byte meta)
	    {
		    id = (int)(stateId >> 4);
		    meta = (byte)(stateId & 0x0F);
		}
	}
}
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Alex.API.Entities;
using Alex.API.Utils;
using Alex.API.World;
using Alex.Blocks.Minecraft;
using Alex.Entities;
using Alex.Graphics.Models.Blocks;
using Alex.Utils;
using Microsoft.Xna.Framework;
using NLog;
using MathF = System.MathF;

namespace Alex.Worlds
{
    public class PhysicsManager : IDisposable
    {
	    private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(PhysicsManager));
	    private IWorld World { get; }

	    public PhysicsManager(IWorld world)
	    {
		    World = world;
	    }

		private ThreadSafeList<IPhysicsEntity> PhysicsEntities { get; } = new ThreadSafeList<IPhysicsEntity>();

		private void TruncateVelocity(IPhysicsEntity entity, float dt)
		{
			if (Math.Abs(entity.Velocity.X) < 0.1 * dt)
				entity.Velocity = new Vector3(0, entity.Velocity.Y, entity.Velocity.Z);
			
			if (Math.Abs(entity.Velocity.Y) < 0.1 * dt)
				entity.Velocity = new Vector3(entity.Velocity.X, 0, entity.Velocity.Z);
			
			if (Math.Abs(entity.Velocity.Z) < 0.1 * dt)
				entity.Velocity = new Vector3(entity.Velocity.X, entity.Velocity.Y, 0);
			
			//entity.Velocity.Clamp();
		}

		Stopwatch sw = new Stopwatch();
		public void Update(GameTime elapsed)
		{
			float dt = ((float) elapsed.ElapsedGameTime.TotalSeconds);
			//if (sw.ElapsedMilliseconds)
			//	dt = (float) sw.ElapsedMilliseconds / 1000f;

			Hit.Clear();
			foreach (var entity in PhysicsEntities.ToArray())
			{
				try
				{
					if (entity is Entity e)
					{
						if (e.NoAi) continue;
						//TruncateVelocity(e, dt);
						
						var velocity = e.Velocity;
						
						if (!e.IsFlying && !e.KnownPosition.OnGround)
						{
							velocity -= new Vector3(0f, (float)(e.Gravity * dt), 0f);
							//var modifier = new Vector3(1f, (float) (1f - (e.Gravity * dt)), 1f);
							//velocity *= modifier;
						}

						var rawDrag = (float) (1f - (e.Drag * dt));
						
						velocity *= new Vector3(rawDrag, 1f, rawDrag);
						
						var position = e.KnownPosition;

						var preview = position.PreviewMove(velocity * dt);

						var boundingBox = e.GetBoundingBox(preview);

						Bound bound = new Bound(World, boundingBox, preview);
						
						if (bound.GetIntersecting(boundingBox, out var blocks))
						{
							velocity = AdjustForY(e.GetBoundingBox(new Vector3(position.X, preview.Y, position.Z)), blocks,
								velocity, position);
							
							//var solid = blocks.Where(b => b.block.Solid && b.box.Max.Y > position.Y).ToArray();
							var solid = blocks.Where(b => b.block.Solid && b.box.Max.Y > position.Y).ToArray();
							Hit.AddRange(solid.Select(x => x.box));

							if (solid.Length > 0)
							{
								var heighest = solid.OrderByDescending(x => x.box.Max.Y).FirstOrDefault();
								if (MathF.Abs(heighest.box.Max.Y - boundingBox.Min.Y) <= 0.5f &&
								    e.KnownPosition.OnGround &&
								    !e.IsFlying)
								{
									//if (!heighest.block.BlockState.Model
									//	.GetIntersecting(heighest.coordinates, boundingBox)
									//	.Any(x => x.Max.Y > heighest.box.Max.Y))
									//if (!blocks.Any(x => x.))
									{

										e.KnownPosition.Y = (float) heighest.box.Max.Y;
									}
								}

								for (float x = 1f; x > 0f; x -= 0.1f)
								{
									Vector3 c = (position - preview) * x + position;
									if (solid.All(s => s.box.Contains(c) != ContainmentType.Contains))
									{
										velocity = new Vector3(c.X - position.X, velocity.Y, c.Z - position.Z);
										break;
									}
								}
							}
						}
						
						e.Velocity = velocity;

						e.KnownPosition.Move(e.Velocity * dt);
						
						TruncateVelocity(e, dt);

						if (MathF.Abs(velocity.Y) < 0.000001f)
						{
							e.KnownPosition.OnGround = true;
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warn(ex, $"Entity tick threw exception: {ex.ToString()}");
				}
			}

			if (Hit.Count > 0)
			{
				LastKnownHit = Hit.ToArray();
			}
			
			sw.Restart();
		}

		private Vector3 AdjustForY(BoundingBox box, (BlockCoordinates coordinates, Block block, BoundingBox box)[] blocks, Vector3 velocity, PlayerLocation position)
		{
			if (velocity.Y == 0f)
				return velocity;
			
			float? collisionPoint = null;
			bool negative = velocity.Y < 0f;
			foreach (var corner in box.GetCorners())
			{
				foreach (var block in blocks)
				{
					if (block.block.Solid && block.box.Contains(corner) == ContainmentType.Contains)
					{
						var heading = corner - position;
						var distance = heading.LengthSquared();
						var direction = heading / distance;

						if (negative)
						{
							if (collisionPoint == null || block.box.Max.Y > collisionPoint.Value)
							{
								collisionPoint = block.box.Max.Y;
							}
						}
						else
						{
							if (collisionPoint == null || block.box.Min.Y < collisionPoint.Value)
							{
								collisionPoint = block.box.Min.Y;
							}
						}
					}
				}
			}

			if (collisionPoint.HasValue)
			{
				float distance = 0f;
				/*if (negative)
				{
					distance = -(box.Min.Y - collisionPoint.Value);
				}
				else
				{
					distance = collisionPoint.Value - box.Max.Y;
				}*/
				
				velocity = new Vector3(velocity.X, distance, velocity.Z);
			}

			return velocity;
		}
		
		public List<BoundingBox> Hit { get; set; } = new List<BoundingBox>();
		public BoundingBox[] LastKnownHit { get; set; } = null;
		public void Stop()
	    {
		  //  Timer.Change(Timeout.Infinite, Timeout.Infinite);
	    }

	    public void Dispose()
	    {
		   // Timer?.Dispose();
	    }

	    public bool AddTickable(IPhysicsEntity entity)
	    {
		    return PhysicsEntities.TryAdd(entity);
	    }

	    public bool Remove(IPhysicsEntity entity)
	    {
		    return PhysicsEntities.Remove(entity);
	    }

	    private class Bound
	    {
		    private Dictionary<BlockCoordinates, (Block block, BoundingBox box)> Blocks = new Dictionary<BlockCoordinates, (Block block, BoundingBox box)>();
		    
		    public Bound(IWorld world, BoundingBox box, Vector3 entityPos)
		    {
			    var min = box.Min;
			    var max = box.Max;
			
			    var minX = (int) Math.Floor(min.X);
			    var maxX = (int) Math.Ceiling(max.X);

			    var minZ = (int) Math.Floor(min.Z);
			    var maxZ = (int) Math.Ceiling(max.Z);

			    var minY = (int) Math.Floor(min.Y);
			    var maxY = (int) Math.Ceiling(max.Y);

			    for (int x = minX; x < maxX; x++)
			    for (int y = minY; y < maxY; y++)
			    for (int z = minZ; z < maxZ; z++)
			    {
				    var coords = new BlockCoordinates(new Vector3(x,y,z));
				    if (!world.HasBlock(coords.X, coords.Y, coords.Z))
					    continue;
					    
				    if (!Blocks.TryGetValue(coords, out _))
				    {
					    var block = GetBlock(world, coords, entityPos);
					    if (block != default)
					    Blocks.TryAdd(coords, block);
				    }
			    }
		    }

		    private (Block block, BoundingBox box) GetBlock(IWorld world, BlockCoordinates coordinates, Vector3 entityPos)
		    {
			    var block = world.GetBlock(coordinates) as Block;
			    if (block == null) return default;
			    
			    //var entityBlockPos = new BlockCoordinates(entityPos);

			    var box = block.GetBoundingBox(coordinates);

			    //var height = (float)block.GetHeight(entityPos - box.Min);
			    //box.Max = new Vector3(box.Max.X, box.Min.Y + height, box.Max.Z);
			    return (block, box);
		    }

		    public IEnumerable<(Block block, BoundingBox box)> GetPoints()
		    {
			    foreach (var b in Blocks)
			    {
				    yield return b.Value;
			    }
		    }

		    public bool GetIntersecting(BoundingBox box, out (BlockCoordinates coordinates, Block block, BoundingBox box)[] blocks)
		    {
			    List<(BlockCoordinates coordinates,Block block, BoundingBox box)> b = new List<(BlockCoordinates coordinates,Block block, BoundingBox box)>();
			    foreach (var block in Blocks)
			    {
				    var vecPos = new Vector3(block.Key.X, block.Key.Y, block.Key.Z);

				    if (block.Value.box.Intersects(box))
				    {
					    /*foreach (var intersect in block.Value.block.BlockState.Model.GetIntersecting(block.Key, box).OrderBy(x => x.Max.Y))
					    {
						    b.Add((block.Value.block, intersect));
						    break;
					    }*/

					    bool added = false;
					    foreach (var point in box.GetCorners().OrderBy(x => x.Y))
					    {
						    var bb = block.Value.block.GetPartBoundingBox(block.Key, point);
						    var bc = bb.Contains(point);
						    if (bc == ContainmentType.Contains)
						    {
							    added = true;
							    b.Add((block.Key, block.Value.block, bb));
							   // break;
						    }
					    }

					    if (!added)
					    {
						    b.Add((block.Key, block.Value.block, block.Value.box));
					    }
				    }
			    }
			    
			    blocks = b.ToArray();
			    return (b.Count > 0);
		    }
		    
		    public bool Intersects(BoundingBox box, out Vector3 collisionPoint, out (Block block, BoundingBox box) block)
		    {
			    foreach (var point in GetPoints())
			    {
				    foreach (var corner in box.GetCorners())
				    {
					    if (point.box.Contains(corner) == ContainmentType.Contains)
					    {
						    collisionPoint = corner;
						    block = point;
						    return true;
					    }
				    }
			    }
			    
			    collisionPoint = default;
			    block = default;
			    return false;
		    }
	    }
    }
}

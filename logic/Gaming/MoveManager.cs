﻿using System;
using GameClass.GameObj;
using GameEngine;

namespace Gaming
{
	public class MoveManager
	{

		//人物移动
		public void MovePlayer(Character playerToMove, int moveTimeInMilliseconds, double moveDirection)
		{
			moveEngine.MoveObj(playerToMove, moveTimeInMilliseconds, moveDirection);
		}

		/*
		private void ActivateMine(Character player, Mine mine)
		{
			gameMap.ObjListLock.EnterWriteLock();
			try { gameMap.ObjList.Remove(mine); }
			catch { }
			finally { gameMap.ObjListLock.ExitWriteLock(); }

			switch (mine.GetPropType())
			{
				case PropType.Dirt:
					player.AddMoveSpeed(Constant.dirtMoveSpeedDebuff, Constant.buffPropTime);
					break;
				case PropType.Attenuator:
					player.AddAP(Constant.attenuatorAtkDebuff, Constant.buffPropTime);
					break;
				case PropType.Divider:
					player.ChangeCD(Constant.dividerCdDiscount, Constant.buffPropTime);
					break;
			}
		}
		*/

		private Map gameMap;
		private MoveEngine moveEngine;
		public MoveManager(Map gameMap)
		{
			this.gameMap = gameMap;
			this.moveEngine = new MoveEngine
			(
				gameMap: gameMap,
				OnCollision: (obj, collisionObj, moveVec) =>
				{
					/*if (collisionObj is Mine)
					{
						ActivateMine((Character)obj, (Mine)collisionObj);
						return MoveEngine.AfterCollision.ContinueCheck;
					}*/
					return MoveEngine.AfterCollision.MoveMax;
				},
				EndMove: obj =>
				{
					//Debugger.Output(obj, " end move at " + obj.Position.ToString() + " At time: " + Environment.TickCount64);
				}
			);
		}
	}
}
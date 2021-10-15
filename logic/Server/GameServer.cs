﻿using System;
using System.Threading;
using System.Collections.Generic;
using Timothy.FrameRateTask;
using Gaming;
using Communication.Proto;
using GameClass.GameObj;

namespace Server
{
    public class GameServer: ServerBase
    {
        protected readonly Game game;
        public override int TeamCount => options.TeamCount;
        protected long[,] communicationToGameID; //通信用的ID映射到游戏内的ID,[i,j]表示team：i，player：j的id。
        private readonly object messageToAllClientsLock = new object();
        private long SendMessageToClientIntervalInMilliseconds = 50;
        private Semaphore endGameInfoSema = new Semaphore(0, 1);
        public override int GetTeamScore(long teamID)
        {
            return game.GetTeamScore(teamID);
        }
        public override void WaitForGame()
        {
            endGameInfoSema.WaitOne();  //开始等待游戏开始
        }
        private uint GetBirthPointIdx(long teamID, long playerID)       //获取出生点位置
        {
            return (uint)(teamID * options.PlayerCountPerTeam + playerID);
        }
        protected readonly object addPlayerLock = new object();
        private bool AddPlayer(MessageToServer msg)
        {
            if (game.GameMap.Timer.IsGaming)  //游戏运行中，不能添加玩家
                return false;
            if (!ValidTeamIDAndPlayerID(msg.TeamID, msg.PlayerID))  //玩家id是否正确
                return false;
            if (communicationToGameID[msg.TeamID, msg.PlayerID] != GameObj.invalidID)  //是否已经添加了该玩家
                return false;

            Preparation.Utility.PassiveSkillType passiveSkill;
            switch(msg.PSkill)
            {
                case PassiveSkillType.Vampire:
                    passiveSkill = Preparation.Utility.PassiveSkillType.Vampire;
                    break;
                case PassiveSkillType.SpeedUpWhenLeavingGrass:
                    passiveSkill = Preparation.Utility.PassiveSkillType.SpeedUpWhenLeavingGrass;
                    break;
                case PassiveSkillType.RecoverAfterBattle:
                    passiveSkill = Preparation.Utility.PassiveSkillType.RecoverAfterBattle;
                    break;
                default:
                    passiveSkill = Preparation.Utility.PassiveSkillType.Null;
                    break;
            }
            Preparation.Utility.ActiveSkillType commonSkill;
            switch(msg.ASkill1)
            {
                case ActiveSkillType.SuperFast:
                    commonSkill = Preparation.Utility.ActiveSkillType.SuperFast;
                    break;
                case ActiveSkillType.NuclearWeapon:
                    commonSkill = Preparation.Utility.ActiveSkillType.NuclearWeapon;
                    break;
                case ActiveSkillType.BecomeVampire:
                    commonSkill = Preparation.Utility.ActiveSkillType.BecomeVampire;
                    break;
                case ActiveSkillType.BecomeAssassin:
                    commonSkill = Preparation.Utility.ActiveSkillType.BecomeAssassin;
                    break;
                default:
                    commonSkill = Preparation.Utility.ActiveSkillType.Null;
                    break;
            }
            lock (addPlayerLock)
            {
                Game.PlayerInitInfo playerInitInfo = new Game.PlayerInitInfo(GetBirthPointIdx(msg.TeamID, msg.PlayerID), msg.TeamID, passiveSkill, commonSkill);
                long newPlayerID = game.AddPlayer(playerInitInfo);
                if (newPlayerID == GameObj.invalidID)
                    return false;
                communicationToGameID[msg.TeamID, msg.PlayerID] = newPlayerID;
            }
            return true;
        }
        private void SendAddPlayerResponse(MessageToServer msgRecieve, bool isValid)
        {
            //if(msgRecieve.PlayerID==2021&&msgRecieve.TeamID==2021)
            //{
            //    //观战模式
            //}

            

            MessageToOneClient msgSend = new MessageToOneClient();
            
            //这里应该是long，怎么成int了？
            msgSend.PlayerID = (int)msgRecieve.PlayerID;
            msgSend.TeamID = (int)msgRecieve.TeamID;

            msgSend.MessageType = MessageType.InitialLized;  //应该要发个信息回去告诉client连上了吗,proto没写好，要改
            //这里proto没写好，得改
            serverCommunicator.SendToClient(msgSend);
            
            if (isValid)
            {
                Console.WriteLine("A new player with teamID {0} and playerID {1} joined the game.", msgRecieve.TeamID, msgRecieve.PlayerID);
            }
            else
            {
                Console.WriteLine("The request of a player declaring to have teamID {0} and playerID {1} to join the game has been rejected.", msgRecieve.TeamID, msgRecieve.PlayerID);
            }

            lock (addPlayerLock)
            {
                CheckStart();       //检查是否该开始游戏了
            }
        }
        protected override void OnReceive(MessageToServer msg)
        {
#if DEBUG 
            Console.WriteLine($"Receive message: from teamID {msg.TeamID} , playerID {msg.PlayerID}: {msg.MessageType}, args: {msg.TimeInMilliseconds} {msg.Angle}");
#endif
            if (msg.TimeInMilliseconds < 0)
            {
                if (msg.Angle >= 0.0) msg.Angle -= Math.PI;
                else msg.Angle += Math.PI;
                if (msg.TimeInMilliseconds == int.MinValue) msg.TimeInMilliseconds = int.MaxValue;
                else msg.TimeInMilliseconds = -msg.TimeInMilliseconds;
            }
            if (double.IsNaN(msg.Angle) || double.IsInfinity(msg.Angle))
                msg.Angle = 0.0;

            switch(msg.MessageType)
            {
                case MessageType.AddPlayer:
                    SendAddPlayerResponse(msg, AddPlayer(msg));
                    break;
                case MessageType.Move:
                    if (ValidTeamIDAndPlayerID(msg.TeamID, msg.PlayerID))
                    {
                        game.MovePlayer(communicationToGameID[msg.TeamID, msg.PlayerID], msg.TimeInMilliseconds, msg.Angle);
                    }
                    break;
                case MessageType.Attack:
                    if(ValidTeamIDAndPlayerID(msg.TeamID,msg.PlayerID))
                    {
                        game.Attack(communicationToGameID[msg.TeamID, msg.PlayerID], msg.Angle);
                    }
                    break;
                case MessageType.UseCommonSkill:
                    if (ValidTeamIDAndPlayerID(msg.TeamID, msg.PlayerID))
                    {
                        //这里返回了是否成功使用技能的值，应该需要发给client
                        bool isSuccess = game.UseCommonSkill(communicationToGameID[msg.TeamID, msg.PlayerID]);
                    }
                    break;

                //可能还有很多类型，只是我不知道该怎么写，先写着这一点先
                default:
                    break;
            }
        }
        private bool ValidTeamIDAndPlayerID(long teamID,long playerID)
        {
            return teamID >= 0 && teamID < options.TeamCount && playerID >= 0 && playerID < options.PlayerCountPerTeam;
        }
        private void SendMessageToAllClients(MessageType msgType, bool requiredGaming=true)
        {
            var gameObjList=game.GetGameObj();
            Google.Protobuf.Collections.RepeatedField<MessageToRefresh> messageToRefresh = new Google.Protobuf.Collections.RepeatedField<MessageToRefresh>();

            /*
            proto要改，这部分先放着，proto改好才能写。
            其他的大致已经写好了
            */







        }
        private void OnGameEnd()
        {
            SendMessageToAllClients(MessageType.EndGame, false);
#if DEBUG
            //跑起来时测试用
            for (int i = 0; i < TeamCount; i++)
                Console.WriteLine($"Team{i} Score: {game.GetTeamScore(game.TeamList[i].TeamID)}");
#endif
            endGameInfoSema.Release();
        }
        private void CheckStart()
        {
            if (game.GameMap.Timer.IsGaming)
                return;
            foreach (var id in communicationToGameID)
            {
                if (id == GameObj.invalidID) return;     //如果有未初始化的玩家，不开始游戏
            }
            new Thread
            (
                () =>
                {
                    game.StartGame((int)options.GameTimeInSecond * 1000);
                    OnGameEnd();
                }
            )
            { IsBackground = true }.Start();

            while (!game.GameMap.Timer.IsGaming) 
                Thread.Sleep(1); //游戏未开始，等待
            SendMessageToAllClients(MessageType.StartGame);     //发送开始游戏信息

            //定时向client发送游戏情况
            new Thread
            (
                () =>
                {
                    //用一次frameratetask膜一次 ↓
                    FrameRateTaskExecutor<int> xfgg = new FrameRateTaskExecutor<int>
                    (
                        () => game.GameMap.Timer.IsGaming,
                        () => SendMessageToAllClients(MessageType.Gaming),
                        SendMessageToClientIntervalInMilliseconds,
                        () => 0
                    )
                    {
                        AllowTimeExceed = true,
                        MaxTolerantTimeExceedCount = 5,
                        TimeExceedAction = overExceed =>
                        {
                            if (overExceed)
                            {
                                Console.WriteLine("Fetal error: your computer runs too slow that server cannot send message at a frame rate of 20!!!");
                            }
#if DEBUG
                            else
                            {
                                Console.WriteLine("Debug info: Send message to clients time exceed for once.");
                            }
#endif
                        }
                    };

                    xfgg.Start();
#if DEBUG
                    new Thread
                        (
                            () =>
                            {
                                while (!xfgg.Finished)
                                {
                                    Console.WriteLine($"Send message to clients frame rate: {xfgg.FrameRate}");
                                    Thread.Sleep(1000);
                                }
                            }
                        )
                    { IsBackground = true }.Start();
#endif
                }
            )
            { IsBackground = true }.Start();
        }
        public GameServer(ArgumentOptions options): base(options)
        {
            this.game = new Game(MapInfo.defaultMap, options.TeamCount);
            communicationToGameID = new long[options.TeamCount, options.PlayerCountPerTeam];
            //创建server时先设定待加入人物都是invalid
            for(int i=0;i<communicationToGameID.GetLength(0);i++)
            {
                for(int j=0;j<communicationToGameID.GetLength(j);j++)
                {
                    communicationToGameID[i, j] = GameObj.invalidID;
                }
            }

            //只要跑起来的话，这部分好像用不到，而且我还不会...
            //if (options.FileName != DefaultArgumentOptions.FileName)
            //{
            //    try
            //    {
            //        mwr = new MessageWriter(options.FileName, options.TeamCount, options.PlayerCountPerTeam);
            //    }
            //    catch
            //    {
            //        Console.WriteLine($"Error: Cannot create the playback file: {options.FileName}!");
            //    }
            //}

            //if (options.Token != DefaultArgumentOptions.Token && options.Url != DefaultArgumentOptions.Url)
            //{
            //    httpSender = new HttpSender(options.Url, options.Token, "PUT");
            //}
        }
    }
}
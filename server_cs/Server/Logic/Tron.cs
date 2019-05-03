﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using Server.Enum;
using Server.Logic.Event;
using Server.Protocol;

namespace Server.Logic
{
    /// <summary>
    /// Game instance.
    /// </summary>
    public class Tron
    {
        #region fields
        private const double FramesPerMillisecond = 1000d / 30d;
        /// <summary>
        /// Size of the quadratic field.
        /// </summary>
        private int _FliedSize;

        Random _Random = new Random();
        int _FramesToNextTail = 20;
        int _Length = 1;
        byte _PlayerSize = 10;
        byte _SegmentSize = 5;
        byte _JumpLength = 16;
        byte _MoveSpeed = 3;

        List<ExtendedPlayer> _Players;
        private byte _Starter = 0;
        CancellationTokenSource _CancellationTokenSource;
        #endregion

        #region properties

        #endregion

        #region ctor
        /// <summary>
        /// Ctor.
        /// </summary>
        public Tron(int fieldSize)
        {
            _FliedSize = fieldSize;
            _Players = new List<ExtendedPlayer>();
            _CancellationTokenSource = new CancellationTokenSource();
        }
        #endregion

        #region public functions

        /// <summary>
        /// Reguster player.
        /// </summary>
        /// <returns>Start position.</returns>
        public void RegisterPlayer(Player player)
        {
            ExtendedPlayer extendedPlayer = new ExtendedPlayer(player);
            extendedPlayer.Death = false;
            extendedPlayer.Length = 0;

            Position position = new Position();
            int halfSize = _FliedSize / 2;
            switch (_Starter)
            {
                case 0:
                    position.X = halfSize + (_PlayerSize / 2);
                    position.Y = 0;
                    extendedPlayer.Direction = Direction.Up;
                    break;
                case 1:
                    position.X = _PlayerSize;
                    position.Y = halfSize - (_PlayerSize / 2);
                    extendedPlayer.Direction = Direction.Right;
                    break;
                case 2:
                    position.X = _FliedSize - _PlayerSize;
                    position.Y = halfSize - (_PlayerSize / 2);
                    extendedPlayer.Direction = Direction.Left;
                    break;
                case 3:
                    position.X = halfSize + (_PlayerSize / 2);
                    position.Y = _FliedSize;
                    extendedPlayer.Direction = Direction.Down;
                    break;
                default:
                    break;
            }
            _Starter++;
            player.Position = position;
            _Players.Add(extendedPlayer);
            return;
        }

        /// <summary>
        /// Start a new game in a new thread.
        /// </summary>
        public void StartGameLoop()
        {
            Task gameThread = Task.Run(() => GameLoop(), _CancellationTokenSource.Token);
        }

        public void StopGameLoop()
        {
            _CancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Process action.
        /// </summary>
        /// <param name="protocol"></param>
        public void ProcessInput(Protocol.Protocol protocol, int playerId)
        {
            // Get player
            ExtendedPlayer player = _Players.FirstOrDefault(p => p.Player.Id == playerId);

            switch (protocol.Action)
            {
                case Protocol.Action.ACT_DOWN:
                    if (player.Direction == Direction.Up)
                        break;
                    player.Direction = Direction.Down;
                    break;
                case Protocol.Action.ACT_JUMP:
                    if (player.Jumping == 0)
                    {
                        player.Jumping = _JumpLength;
                        player.Player.Position.Jumping = true;
                    }
                    break;
                case Protocol.Action.ACT_LEFT:
                    if (player.Direction == Direction.Right)
                        break;
                    player.Direction = Direction.Left;
                    break;
                case Protocol.Action.ACT_RIGHT:
                    if (player.Direction == Direction.Left)
                        break;
                    player.Direction = Direction.Right;
                    break;
                case Protocol.Action.ACT_UP:
                    if (player.Direction == Direction.Down)
                        break;
                    player.Direction = Direction.Up;
                    break;
            }

        }
        #endregion

        #region private functions

        /// <summary>
        /// Get the current time in milliseconds
        /// </summary>
        /// <returns></returns>
        private double GetCurrentTime()
        {
            return DateTime.UtcNow.Millisecond;
        }

        /// <summary>
        /// Collect data and provides.
        /// </summary>
        /// <param name="type"></param>
        private void Broadcast(Protocol.Type type = Protocol.Type.TYPE_UPDATE)
        {
            // Create snapshot.
            Protocol.Protocol protocol = new Protocol.Protocol
            {
                Type = type,
                Length = _Length
            };

            foreach (ExtendedPlayer player in _Players)
            {
                if (player.Jumping > 0)
                    player.Player.Position.Jumping = true;
                else
                    player.Player.Position.Jumping = false;

                protocol.Players.Add(player.Player);
            }

            // Provide for broadcasting
            SnapshotCreated?.Invoke(new SnapshotArguments(protocol));
        }

        /// <summary>
        /// Game loop.
        /// </summary>
        private void GameLoop()
        {
            Broadcast(Protocol.Type.TYPE_START);

            // Time for the client to build the interface(gui).
            Thread.Sleep(2000);

            bool end = false;

            while (!end)
            {
                Thread.Sleep((int)FramesPerMillisecond);
                Update();
                end = CheckGameEnd();
                Broadcast();
            }

            int winner = 0;
            var lastManStanding = _Players.FirstOrDefault(p => p.Death == false);
            if (lastManStanding != null)
                winner = lastManStanding.Player.Id;
            else
                winner = _Players.First(ep => ep.Length ==_Players.Select(p => p.Length).Max()).Player.Id;

            GameEnded?.Invoke(new GameEndedArguments(winner));
        }

        private void DetectCollisions()
        {
            foreach (var playerA in _Players)
            {
                if (playerA.Death)
                    continue;

                if (playerA.Jumping > 0)
                    continue;

                foreach (var playerB in _Players)
                {
                    if (playerA.Equals(playerB))
                        continue;

                    if (playerB.Death)
                        continue;

                    var headA = playerA.Player.Position;
                    var headArrayA = GetPlayerHead(headA.X, headA.Y);

                    var headB = playerB.Player.Position;
                    var headArrayB = GetPlayerHead(headB.X, headB.Y);

                    // detect head on head collision
                    foreach (Point pointA in headArrayA)
                    {
                        // optimize: if headB is not in range => skip
                        if (headB.X > (headA.X + _PlayerSize)
                            || (headB.X + _PlayerSize) < headA.X

                            || (headB.Y - _PlayerSize) > headA.Y
                            || headB.Y < (headA.Y - _PlayerSize))
                            break;


                        foreach (Point pointB in headArrayB)
                        {
                            // One player is jumping
                            if (((playerA.Jumping > 0) && !(playerB.Jumping > 0))
                                || ((playerB.Jumping > 0) && !(playerA.Jumping > 0)))
                                break;

                            if (pointA.X == pointB.X 
                                && pointA.Y == pointB.Y)
                            {
                                // head on head => both players killed
                                IAmKilled(playerA.Player.Id);
                                IAmKilled(playerB.Player.Id);
                                break;
                            }
                        }
                    }

                    // detect head on tail collision
                    // player has tail
                    if (playerB.Length > _PlayerSize)
                    {
                        foreach (Point segmentPart in playerB.Tail)
                        {
                            if (playerA.Death)
                                break;

                            // optimize: if head is not in range of segment => skip
                            if (segmentPart.X > (headA.X + _PlayerSize)
                                || (segmentPart.X + _PlayerSize) < headA.X

                                || (segmentPart.Y - _PlayerSize) > headA.Y
                                || segmentPart.Y < (headA.Y - _PlayerSize))
                                break;

                            var segmentArray = GetSegment(segmentPart.X, segmentPart.Y);
                            foreach (Point head in headArrayA)
                            {
                                if (playerA.Death)
                                    break;

                                foreach (Point segmentPoint in segmentArray)
                                {
                                    if (segmentPoint.X == head.X
                                        && segmentPoint.Y == head.Y)
                                    {
                                        IAmKilled(playerA.Player.Id);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        private void Update()
        {
            // Prcocess game.
            // Move players.
            Move();
            DetectCollisions();
            _FramesToNextTail--;

            if (_FramesToNextTail == 0)
            {
                _FramesToNextTail = _Random.Next(5, 26);
                _Length++;

                foreach (var player in _Players)
                {
                    player.Length++;

                    if (player.Jumping > 0)
                        player.Player.Position.Jumping = true;
                    else
                        player.Player.Position.Jumping = false;
                }
            }

        }

        private bool CheckGameEnd()
        {
            var actives = _Players.Where(p => p.Death == false);
            if (actives.Count() <= 1)
            {
                // game end == true
                return true;
            }
            return false;
        }

        private void IAmKilled(int id)
        {
            _Players.First(p => p.Player.Id == id).Death = true;
            PlayerDied?.Invoke(new DeathArguments(id));
        }

        private void Move()
        {
            foreach (ExtendedPlayer player in _Players)
            {
                if (player.Death)
                    continue;

                player.Tail.Enqueue(new Point(player.Player.Position.X, player.Player.Position.Y));

                switch (player.Direction)
                {
                    case Direction.Down:
                        player.Player.Position.Y += _MoveSpeed;
                        player.Player.Position.Y = Teleport(player.Player.Position.Y);
                        break;
                    case Direction.Left:
                        player.Player.Position.X -= _MoveSpeed;
                        player.Player.Position.X = Teleport(player.Player.Position.X);
                        break;
                    case Direction.Right:
                        player.Player.Position.X += _MoveSpeed;
                        player.Player.Position.X = Teleport(player.Player.Position.X);
                        break;
                    case Direction.Up:
                        player.Player.Position.Y -= _MoveSpeed;
                        player.Player.Position.Y = Teleport(player.Player.Position.Y);
                        break;
                }

                while (player.Tail.Count > _Length)
                {
                    player.Tail.Dequeue();
                }

                if (player.Jumping > 0)
                    player.Jumping--;
            }
        }

        private int Teleport(int pos)
        {
            if (pos < 0)
                return _FliedSize;
            else if (pos > _FliedSize)
                return 0;
            return pos;
        }

        private List<Point> GetPlayerHead(int x, int y)
        {
            // x,y are top left
            // player size is _PlayerSize x _PlayerSize
            // only gets the frame
            List<Point> result = new List<Point>();

            for (int ix = 0; ix < _PlayerSize; ix++)
            {
                result.Add(new Point(x + ix, y));
                result.Add(new Point(x + ix, y - _PlayerSize));
            }

            for (int iy = 1; iy < _PlayerSize; iy++)
            {
                result.Add(new Point(x, iy));
                result.Add(new Point(x + _PlayerSize, iy));
            }

            return result;
        }

        private List<Point> GetSegment(int x, int y)
        {
            // x,y are top left
            // segment size is _SegmentSize x _SegmentSize
            // only gets the frame
            List<Point> result = new List<Point>();

            for (int ix = 0; ix < _SegmentSize; ix++)
            {
                result.Add(new Point(x + ix, y));
                result.Add(new Point(x + ix, y - _SegmentSize));
            }

            for (int iy = 1; iy < _SegmentSize; iy++)
            {
                result.Add(new Point(x, iy));
                result.Add(new Point(x + _SegmentSize, iy));
            }

            return result;
        }

        #endregion

        #region events
        /// <summary>
        /// Occurs when a new snapshot is created.
        /// </summary>
        /// <param name="s">SnapshotArguments.</param>
        public delegate void SnapshotHandler(SnapshotArguments s);
        /// <summary>
        /// Occurs when a new snapshot is created.
        /// </summary>
        public event SnapshotHandler SnapshotCreated;


        public delegate void PlayerDeathHandler(DeathArguments d);
        public event PlayerDeathHandler PlayerDied;

        public delegate void GameEndedHandler(GameEndedArguments g);
        public event GameEndedHandler GameEnded;
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Reflection.Emit;

namespace LoadRoom
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("start loadroom");
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);
            try
            {
                LoadRoom(args[0], args[1]);
            }
            catch (Exception e) { Console.WriteLine(e); }
        }

        public struct RoomData
        {
            public int[,][] geometry;
            public int width;
            public int height;
            public int waterLevel;

            public RoomData(string[] lines)
            {
                string[] array = lines[1].Split('|');
                width = int.Parse(array[0].Split('*')[0]);
                height = int.Parse(array[0].Split('*')[1]);
                if (array.Length > 1) waterLevel = int.Parse(array[1]);
                else waterLevel = -1;
                geometry = GeometryArray(lines[11].Split('|'), width, height);
            }
        }

        static void LoadRoom(string filePath, string savePath)
        {
            if (filePath[0] == '"')
            {
                filePath = filePath.Substring(1, filePath.Length - 2);
            }
            RoomData room = new(File.ReadAllLines(filePath));
            Console.WriteLine($"loaded file from {filePath}");
            Bitmap bmp = BitmapFromGeometry(room);

            List<string> connections = ConnectionsProcessor(room, bmp);

            SaveRoomData(savePath + ".txt", room, connections);

            bmp.Save(savePath + ".png", System.Drawing.Imaging.ImageFormat.Png);
            bmp.Dispose();
        }

        static void SaveRoomData(string path, RoomData room, List<string> connections)
        {
            List<string> lines = new() { $"{room.width},{room.height}" };
            foreach (string connection in connections)
            { lines.Add(connection); }

            File.WriteAllLines(path, lines.ToArray());
            Console.WriteLine($"saved file to {path}");
        }

        static int[,][] GeometryArray(string[] geometry, int width, int height)
        {
            int[,][] geo = new int[width, height][];
            int i = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++, i++)
                {
                    geo[x, y] = geometry[i].Split(',').Select(int.Parse).ToArray();
                }
            }
            return geo;
        }

        static Bitmap BitmapFromGeometry(RoomData room)
        {
            Bitmap bmp = new(room.width, room.height);

            int i = 0;

            for (int x = 0; x < room.width; x++)
            {
                for (int y = 0; y < room.height; y++, i++)
                {
                    int[] split = room.geometry[x, y];
                    Color color = split[0] switch
                    {
                        0 when split.Skip(1).Any(x => x is 1 or 2) => Color.FromArgb(128, 77, 77),
                        0 when split.Skip(1).Any(x => x is 6) => Color.FromArgb(128, 128, 128),
                        0 => Color.FromArgb(153, 153, 153),
                        1 => Color.FromArgb(77, 77, 77),
                        2 or 3 => Color.FromArgb(128, 77, 77),
                        4 => Color.FromArgb(77, 150, 77),
                        _ => Color.FromArgb(255, 255, 255)
                    };

                    if (y > room.height - room.waterLevel - 2)
                    {
                        color = Color.FromArgb((int)(color.R * 0.7f), (int)(color.G * 0.7f), 255 - (int)((255 - color.B) * 0.7f));
                    }

                    bmp.SetPixel(x, y, color);

                    //Console.WriteLine($"x: {x}, y: {y}, color: {color}");
                }
            }

            return bmp;
        }

        static List<string> ConnectionsProcessor(RoomData room, Bitmap bmp)
        {
            List<Tuple<int, int>> nodeStart = new();
            for (int x = 0; x < room.width; x++)
            {
                for (int y = 0; y < room.height; y++)
                {
                    if (room.geometry[x, y][0] == 4)
                    {
                        nodeStart.Add(new(x, y));
                    }
                }
            }

            List<NodeProcessor> nodes = new();
            foreach (Tuple<int, int> pos in nodeStart)
            { nodes.Add(new(room, pos.Item1, pos.Item2)); }

            nodes = nodes.OrderBy(x => x.Type).ThenBy(x => x.EndY).ThenBy(x => x.EndX).ToList();

            foreach (NodeProcessor node in nodes)
            {
                Console.WriteLine(node.ToString());
                Color color = node.Type switch
                {
                    NodeProcessor.ShortcutType.Entrance => Color.FromArgb(255, 255, 0),
                    NodeProcessor.ShortcutType.Den => Color.FromArgb(255, 128, 0),
                    NodeProcessor.ShortcutType.Scav => Color.FromArgb(50, 50, 50),
                    NodeProcessor.ShortcutType.Garbage => Color.FromArgb(128, 37, 0),
                    NodeProcessor.ShortcutType.Shortcut => Color.FromArgb(64, 192, 0),
                    NodeProcessor.ShortcutType.Wack => Color.FromArgb(254, 0, 0),
                    NodeProcessor.ShortcutType.Dead => Color.FromArgb(192, 0, 192),
                    _ => Color.FromArgb(77, 150, 77)
                };
                bmp.SetPixel(node.StartX, node.StartY, color);
            }

            return nodes.Select(x => x.ToString()).ToList();
        }

        public class NodeProcessor
        {
            int X;
            int Y;
            int lastX;
            int lastY;
            readonly RoomData room;

            public int StartX;
            public int StartY;
            public int EndX;
            public int EndY;
            public int FirstDirection;
            public ShortcutType Type = ShortcutType.Dead;


            public NodeProcessor(RoomData room, int x, int y)
            {
                this.X = x;
                this.Y = y;
                this.lastX = x;
                this.lastY = y;
                this.room = room;

                FirstDirection = -1;

                MapShortcut();

                StartX = x;
                StartY = y;

                EndX = X;
                EndY = Y;
            }

            public override string ToString()
            {
                return $"{StartX},{StartY},{EndX},{EndY},{(int)Type},{FirstDirection}";
            }

            public void MapShortcut()
            {
                for (int i = 0; i < 10000; i++)
                {
                    Tuple<int, int> nextPos = FindNext();

                    if (nextPos != null)
                    {
                        lastX = X;
                        lastY = Y;
                        X = nextPos.Item1;
                        Y = nextPos.Item2;
                    }

                    ShortcutType? stash = Stash(nextPos);
                    if (stash != null)
                    { Type = (ShortcutType)stash; return; }

                    if (FirstDirection == -1)
                    { FirstDirection = (0 - (X - lastX) + 1) + Math.Max(0, 0 - (lastY - Y)) * 2; }
                }
                //some error occurred
            }

            public ShortcutType? Stash(Tuple<int, int> nextPos)
            {
                if (nextPos == null)
                { return Type = ShortcutType.Dead; }

                int[] pos = room.geometry[nextPos.Item1, nextPos.Item2];

                if (pos[0] == 4)
                { return Type = ShortcutType.Shortcut; }

                if (pos.Skip(1).Contains(4))
                { return Type = ShortcutType.Entrance; }

                if (pos.Skip(1).Contains(5))
                { return Type = ShortcutType.Den; }

                if (pos.Skip(1).Contains(9))
                { return Type = ShortcutType.Wack; }

                if (pos.Skip(1).Contains(12))
                { return Type = ShortcutType.Scav; }

                if (pos.Skip(1).Contains(4))
                { return Type = ShortcutType.Entrance; }

                return null;
            }

            public Tuple<int, int> FindNext()
            {
                //check forward
                int nextX = X + (X - lastX);
                int nextY = Y + (Y - lastY);

                if (CheckNextNode(nextX, nextY))
                { return new(nextX, nextY); }

                //check left
                nextX = X - 1; nextY = Y;

                if (CheckNextNode(nextX, nextY))
                { return new(nextX, nextY); }

                //check up
                nextX = X; nextY = Y + 1;

                if (CheckNextNode(nextX, nextY))
                { return new(nextX, nextY); }
                 
                //check right
                nextX = X + 1; nextY = Y;

                if (CheckNextNode(nextX, nextY))
                { return new(nextX, nextY); }
                
                //check down
                nextX = X; nextY = Y - 1;

                if (CheckNextNode(nextX, nextY))
                { return new(nextX, nextY); }

                return null;
            }

            public bool CheckNextNode(int x, int y)
            {
                //don't check out of bounds
                if (x < 0 || x >= room.width || y < 0 || y >= room.height)
                { return false; }

                //don't backtrack (would cause infinite loops)
                if (x == lastX && y == lastY)
                { return false; }

                //return if it is a shortcut
                return room.geometry[x, y].Skip(1).Any(a => a == 3);
            }

            public enum ShortcutType
            {
            Entrance = 0,
            Den = 1,
            Scav = 2,
            Garbage = 3,
            Shortcut = 4,
            Wack = 5,
            Dead = 6
            }
        }
    }
}

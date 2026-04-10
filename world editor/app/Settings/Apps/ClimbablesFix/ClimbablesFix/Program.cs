using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

namespace ClimbablesFix
{
    internal class Program
    {
        static void Main(string[] args)
        {
        file:
            Console.WriteLine("please input a filepath");
            string filePath = Console.ReadLine();
            if (Directory.Exists(filePath))
            {
                string[] files = Directory.GetFiles(filePath);
                foreach (string str in files)
                {
                    File.WriteAllLines(str, ParseFile(File.ReadAllLines(str)));
                }
            }
            else if (!File.Exists(filePath)) goto file;

            File.WriteAllLines(filePath, ParseFile(File.ReadAllLines(filePath)));
            Console.WriteLine("done");
            Console.ReadLine();
        }


        static string[] ParseFile(string[] lines)
        {
            Console.WriteLine("method running");
            for (int i = 0; i < lines.Length; i++)
            {
                string[] arr = Regex.Split(lines[i], ": ");
                if (arr.Length < 2 || arr[0] != "PlacedObjects") continue;

                Console.WriteLine("found PlacedObjects");

                string[] arr2 = Regex.Split(arr[1], ", ");

                for (int j = 0; j < arr2.Length; j++)
                {
                    string obj = arr2[j];
                    string[] arr3 = Regex.Split(obj, "><");
                    if (arr3.Length < 4 || arr3[0] != "ClimbableArc") continue;

                    Console.WriteLine("found ClimbableArc");

                    if (arr3[3].Contains("^")) { Console.WriteLine("already updated! skipping..."); continue; }
                    string[] arr4 = Regex.Split(arr3[3], "~");

                    arr3[3] = $"0;0^{arr4[0]};{arr4[1]}^{arr4[2]};{arr4[3]}^{arr4[4]};{arr4[5]}";
                    arr2[j] = string.Join("><", arr3);
                }

                lines[i] = "PlacedObjects: " + string.Join(", ", arr2) + ", ";
            }
            return lines;
        }
    }
}

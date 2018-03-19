using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NXBaseValue
{
    class ParseString
    {
        public static int IndexOfNumber(string str,int StartPos=0)
        {
            if (str == null)
                return -1;
            if (str.Length == 0)
                return -1;
            int index = StartPos;
            for(;index<str.Length;index++)
            {
                if (char.IsNumber(str[index]) || str[index]=='.')
                    return index;
            }
                return -1;
        }
        public static int IndexOfNoSpace(string str, int StartPos = 0)
        {
            if (str == null)
                return -1;
            if (str.Length == 0)
                return -1;
            int index = StartPos;
            for (; index < str.Length; index++)
            {
                if (!(char.IsWhiteSpace(str[index]) || str[index] == '\t'))
                    return index;
            }
            return -1;
        }
        public static string FindString(string str,ref int StartPos)
        {
            if (str == null)
                return null;
            string temp_str = "";
            int index = -1;
            index = IndexOfNoSpace(str, StartPos);
            if (index == -1)
                return temp_str;
            temp_str += str[index];
                for (int i = index; i < str.Length;i++)
            {
                StartPos = i + 1;
                int temp_index = IndexOfNoSpace(str, StartPos);
                if (temp_index == -1)
                    return temp_str;                
                if (temp_index == StartPos)
                    temp_str += str[temp_index];
                else
                    return temp_str;                    
            }
            return temp_str;           
        }
        public static ArrayList FindAllString(string str)
        {
            ArrayList array = new ArrayList();
            if (str == null)
                return array;
            int StartPos = 0;
            int temp_Pos = 0;
            do
            {
                temp_Pos = StartPos;
                string temp_str = FindString(str, ref StartPos);
                if (temp_Pos == StartPos)
                    break;
                else
                {
                    array.Add(temp_str);
                }
            }
            while (StartPos<str.Length);
            return array;
        }
      
    }
}

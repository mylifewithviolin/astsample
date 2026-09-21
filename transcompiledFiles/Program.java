import System;

package BubbleSort;

/**
 * プログラム型
 */
public class Program
{
    /**
     * メイン
     * @param args 引数
     */
    public static void Main(String[] args)
    {
        int[] array = new int[] { 15, 13, 9, 6, 4, 1 };
        BubbleSort(array);
        for (int i = 0; i < array.Length; i++)
        {
            ConsoleOut(array[i]);
        }
    }
    
    /**
     * バブルソートする
     * @param array 配列
     */
    public static void BubbleSort(int[] array)
    {
        int outer = 0;
        while (outer < array.Length)
        {
            int inner = 0;
            while (inner < array.Length - outer - 1)
            {
                if (array[inner] > array[inner + 1])
                {
                    int temp = array[inner];
                     = array[inner + 1];
                     = temp;
                }
                inner++;
            }
            outer++;
        }
    }
    
    /**
     * コンソール表示する
     * @param dispStr 引数2
     */
    public static void ConsoleOut(String dispStr)
    {
        Console.WriteLine(dispStr);
    }
    
}


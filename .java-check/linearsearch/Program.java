package linearsearch;

import java.lang.System;

/**
 * プログラム型
 */
public class Program
{
    /**
     * メイン
     * @param args 引数
     */
    public static void main(String[] args)
    {
        /**
         * 配列
         */
        int[] array = new int[] { 15, 13, 9, 6, 4, 1 };
        /**
         * 探す値
         */
        int target = 4;
        /**
         * 該当インデックス
         */
        int index = linearSearch(array, target);
        if (index != -1)
        {
            System.out.println(index);
        }
        else
        {
            System.out.println("該当なし");
        }
    }
    
    /**
     * 線形探索する
     * @param array 配列
     * @param target 探す値
     */
    public static int linearSearch(int[] array, int target)
    {
        /**
         * インデックス
         */
        int i = 0;
        while (i < array.length)
        {
            if (array[i] == target)
            {
                return i;
            }
            i++;
        }
        return -1;
    }
    
    /**
     * コンソール表示する
     * @param dispStr 引数2
     */
    public static void consoleOut(String dispStr)
    {
        System.out.println(dispStr);
    }
    
}


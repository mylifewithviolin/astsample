package bubblesort;

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
        bubbleSort(array);
        for (int i = 0; i < array.length; i++)
        {
            System.out.println(array[i]);
        }
    }
    
    /**
     * バブルソートする
     * @param array 配列
     */
    public static void bubbleSort(int[] array)
    {
        /**
         * 外側
         */
        int i = 0;
        while (i < array.length)
        {
            /**
             * 内側
             */
            int j = 0;
            while (j < array.length - i - 1)
            {
                if (array[j] > array[j + 1])
                {
                    /**
                     * 一時
                     */
                    int temp = array[j];
                    array[j] = array[j + 1];
                    array[j + 1] = temp;
                }
                j++;
            }
            i++;
        }
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


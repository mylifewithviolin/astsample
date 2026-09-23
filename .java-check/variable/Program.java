package helloworld;

import java.lang.System;

/**
 * プログラム型
 */
public class Program
{
    private static String aisatsu1;
    /**
     * メイン
     * @param args 引数
     */
    public static void main(String[] args)
    {
        aisatsu1 = "Hello World one!";
        System.out.println(aisatsu1);
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


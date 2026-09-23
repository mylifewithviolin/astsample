package mvpvalidation;

import java.lang.System;

/**
 * プログラム型
 */
public class Program
{
    private static final int limit = 3;
    /**
     * メイン
     * @param args 引数
     */
    public static void main(String[] args)
    {
        /**
         * 有効
         */
        bool enabled = true;
        /**
         * 無効
         */
        bool disabled = false;
        /**
         * カウント
         */
        int count = 0;
        /**
         * 結果
         */
        int result = evaluate(enabled, disabled);
        if (result > 0)
        {
            while (count < result)
            {
                System.out.println(count);
                count++;
            }
        }
        else
        {
            System.out.println("disabled");
        }
    }
    
    /**
     * 判定する
     * @param enabled 有効
     * @param disabled 無効
     */
    public static int evaluate(bool enabled, bool disabled)
    {
        if (enabled && !disabled || disabled)
        {
            return limit;
        }
        else
        {
            return -1;
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

package priority01semanticvalid;

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
         * 個数
         */
        int count = 3;
        /**
         * 結果
         */
        int result = verifyValue(count);
        /**
         * 妥当
         */
        boolean valid = result == 3;
        /**
         * メッセージ
         */
        String message = "semantic-ok";
        if (valid)
        {
            System.out.println(message);
        }
        else
        {
            System.out.println("semantic-ng");
        }
    }
    
    /**
     * 値を確認する
     * @param value 値
     */
    public static int verifyValue(int value)
    {
        return value;
    }
    
}


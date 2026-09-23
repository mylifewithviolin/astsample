package Bubblesort;

/**
 * プログラム型
 */
public class Program {

    /**
     * メイン
     * @param args 引数
     */
    public static void Main(String[] args) {

        /** array （配列） */
        int[] array = {15, 13, 9, 6, 4, 1};

        /** index （該当インデックス） */
        Bubblesort(array);

        for (int i = 0; i < array.length; i++) {
            consoleOut(String.valueOf(array[i]));
        }
    }

    /**
     * バブルソートする
     * @param array 配列
     */
    public static void Bubblesort(int[] array) {

        /** i （外側） */
        int outer = 0;

        while (outer < array.length) {

            /** j （内側） */
            int inner = 0;

            while (inner < array.length - outer - 1) {

                if (array[inner] > array[inner + 1]) {

                    /** temp （一時） */
                    int temp = array[inner];
                    array[inner] = array[inner + 1];
                    array[inner + 1] = temp;
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
    public static void consoleOut(String dispStr) {
        System.out.println(dispStr);
    }
}

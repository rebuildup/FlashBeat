using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public class MakeFileSceneManager : MonoBehaviour
{
    private TextData data; // テストデータ用のオブジェクト

    void Start() 
    {
        // テスト用のデータを作成
        data = new TextData
        {
            StartTime = new float[] { 1.580f                ,4.297f              ,7.027f          ,8.781f          ,11.319f      ,12.307f,12.965f,13.448f,13.922f,14.424f,15.300f   ,16.160f          ,18.123f ,18.779f   ,19.555f,20.086f,20.923f         ,22.364f     ,23.543f,23.704f,24.005f,24.389f,24.650f,25.064f,25.243f,25.571f,25.796f,26.152f,26.461f,26.783f,26.956f,27.322f,27.765f,28.002f,28.303f,28.608f,28.752f,29.041f,29.258f,29.371f,29.538f,30.035f,30.594f,30.738f,30.952f,31.267f,31.522f,31.773f,31.940f,32.339f,32.610f,32.807f,33.068f,33.417f,34.234f,34.644f,35.011f,35.403f  ,36.091f,36.701f   ,37.906f  ,38.591f          ,40.082f           ,41.771f             ,43.449f ,44.230f,45.110f    ,45.925f,46.487f  ,47.178f   ,48.215f ,48.888f,49.213f,49.920f         ,51.243f,71.200f     ,71.929f,72.377f,72.872f,73.217f     ,74.460f  ,75.312f        ,77.131f         },
            Furigana = new string[] { },
            Mondai = new string[] {"一つひかるもんがあって" ,"二つひとがつないで","三つおってゆく","ひとおってゆく","ひを奏でろ","実なる","宴に","委ね"  ,"種が","稲穂へ","恵まれた","念はされど0.5合","口では","嘆ずるが","詞に","妬んで","唇を噛んでいる","惑う思いは","戦意","減産"  ,"流星" ,"育児" ,"前祝" ,"帰社","帝室"  ,"暗転","爆撃"  ,"愁殺"  ,"透析","遮断","墨継"  ,"圧縮","仮寝"   ,"荷受","歌謡","欺計"  ,"旗竿"  ,"放管","教師","孤帆","学帽"   ,"蛍草","強膜"  ,"弾奏","果実"  ,"規定","真跡","異変"    ,"惜愛","懐想","袋棚"  ,"武骨","御鍋","待針"    ,"挫け","腐り","薄る"  ,"花信を","誇り","朽ちないで","殺げる","柄じゃないじゃん","再起とかしないで","飽きじゃ飼われんぜ","口説け","詠え"  ,"さあ永を","泳ぎ","汚される","巧として","貢ぐは","負と","危ぶむ","厄になってやる",""     ,"さあ、さあ","願う","類の","お題で","影が憬れて","悦を競い","たったの0.5合","捻っては揺らぎ"},
            romaji = new string[] { },
            EndTime = new float[] { }
        };

        // JSONファイルに書き込む
        WriteJsonToFile(data, "一龠_text.json");
    }

    // JSONファイルに書き込む関数
    private void WriteJsonToFile(TextData data, string fileName)
    {
        string json = JsonUtility.ToJson(data); // データをJSON文字列に変換
        string path = Application.dataPath + "/Resources/" + fileName; // ファイルの保存先パス

        // ファイルに書き込む
        File.WriteAllText(path, json);

        Debug.Log("JSONファイルを作成しました: " + path);
    } 
}
/*
 注意
    このゲームのスクリプト全て絶妙なバランスで成り立っています
    奇跡です
    削除時はコメントアウト 変数は削除しないようにするのが望ましいです
    
 曲追加の手順
    �@RecoursesにノーツのJsonファイル
    �AMakeFileSceneにて歌詞のJsonファイルを作成
        StartTimeとMondaiの欄を埋めてください
        他を埋める意味はなくなりました
        ファイルだけ作成できれば良いのでスクリプトを保存した後 MakeFileSceneを実行してください
    �BGManagerスクリプト以下の項目を変更
    　SongName
      Musician
    　SongUrl
      SBPM
      Slevel
      Shit
    　Hiscore
      SongLong
      totalSong
    �CGameScene>>YoutubePlayer>>複製>>名前をSongIDの数値に変更>>MusicManagerにアタッチ
 */
/*
 * テンプレ     曲名変えるの忘れるな
            StartTime = new float[] {},
            Furigana = new string[] {},
            Mondai = new string[] {},
            romaji = new string[] {},
            EndTime = new float[] {}
 * ロストアンブレラ
            StartTime = new float[] { 0.9459999799728394f, 2.3380000591278078f, 4.271999835968018f, 5.752999782562256f, 7.25600004196167f, 7.7729997634887699f, 9.35200023651123f, 11.345999717712403f, 13.112000465393067f, 34.22800064086914f, 37.71099853515625f, 41.26900100708008f, 44.832000732421878f, 49.50600051879883f, 53.720001220703128f, 54.90800094604492f, 58.75f, 61.76900100708008f, 62.15999984741211f, 63.60300064086914f, 65.37100219726563f, 66.88300323486328f, 68.48400115966797f, 70.60600280761719f, 72.89399719238281f, 74.47100067138672f},
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "僕を連れてって", "浸み込んでしまう前に", "見えないまま掴みたいとか", "どうせ叶わないからさ", "あぁ", "手はまだずっと濡れていて", "いつか落としてしまうこと", "まだ気付いてなかった", "　", "細かい雨が目に浸みるのも", "湿った息が喉に詰まるのも", "容にならないものを背負った僕は", "案外楽だったのかもしんないな", "声になれなかった分だけ", "目の前で", "邪魔している霧雨に", "傘を翳して逃げ惑いたいよ", "　", "僕を連れてって", "浸み込んでしまう前に", "見えないままやられちゃうとか", "どうにも出来ないからさ", "離せない手はずっと濡れていて", "いつか落としてしまうこと", "まだ気付いてなかった", "" },
            romaji = new string[] { "bokuwoturetette", "simikonndesimaumaeni", "mienaimamatukamitaitoka", "dousekanawanaikarasa", "ala", "tehamadazuttonureteite", "itukaotositesimaukoto", "madakizuitenakatta", "", "komakaiamegamenisimirunomo", "simettaikiganodonitumarunomo", "katatininaranaimonowoseottabokuha", "anngairakudattanokamosinnnaina", "koeninarenakattabunndake", "menomaede", "zyamasiteirukirisameni", "kasawokazasitenigemadoitaiyo", "", "bokuwoturetette", "simikondesimaumaeni", "mienaimamayararetyautoka", "dounimodekinaikarasa", "hanasenaitehazuttonureteite", "itukaotositesimaukoto", "madakizuitenakatta", "" },
            EndTime = new float[] { 2.299999952316284f, 4.199999809265137f, 5.69999980926513f, 7.199999809265137f, 7.699999809265137f, 9.300000190734864f, 11.300000190734864f, 13.10000038146972f, 34.20000076293945f, 37.599998474121097f, 41.20000076293945f, 44.79999923706055f, 49.400001525878909f, 53.70000076293945f, 54.79999923706055f, 58.70000076293945f, 61.7000007629394f, 62.099998474121097f, 63.5f, 65.30000305175781f, 66.80000305175781f, 68.4000015258789f, 70.5f, 72.80000305175781f, 74.4000015258789f }
 
* エウタナシア
            StartTime = new float[] {14.676f,18.185f,22.001f,25.627f,29.534f,30.155f,33.228f,35.202f,37.063f,37.716f,40.867f,44.407f,44.819f,46.658f,48.313f,50.185f,52.331f,53.943f,55.969f,57.298f,59.435f,59.806f,61.434f,63.386f,65.236f,67.318f,69.230f,70.797f,74.949f},
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "香水に揺蕩う澱を見て", "失った言葉をよく噛んで", "思いつく限りの幼さを", "ひと匙の砂糖とよく混ぜて", "それもまた", "丁寧に 丁寧に 丁寧に 丁寧にと", "継ぎはぎだらけのフィラメント", "有り余る程ならいらないよ", "一つずつ", "丁寧に 丁寧に 丁寧に 丁寧にと", "次で行き止まりの気がした", "何も", "愛してくれ 愛してくれ", "愛してくれとは言わない", "異常なくらいの優しさを", "すこし分け与えてくれないか", "幸せと感じたあの日は", "もっとでたらめなままで良かった", "その心地よさが", "あたしの首を絞めているんだ", "総て", "返してくれ 返してくれ", "返してくれとは言わない", "思想を喰らう浅ましさを", "哀れみで誤魔化して了うなら", "卑怯と言われても仕方ない", "エレジーに情なんて湧かない", "その目を閉じて笑おうか", "　" },
            romaji = new string[] { "kousuinitayutauoriwomite", "usinattakotobawoyokukande", "omoitukukagirinoosanasawo", "hitosazinosatoutoyokumazete", "soremomata", "teineiniteineiniteineiniteineinito", "tugihagidarakenofiramento", "ariamaruhodonarairanaiyo", "hitotuzutu", "teineiniteineiniteineiniteineinito", "tugideikidomarinokigasita", "nanimo", "aisitekureaisitekure", "aisitekuretoohaiwanai", "izyounakurainoyasasisawo", "sukosiwakeataetekuernaika", "siawasetokanzitaanohiha", "mottodetaramenamamadeyokatta", "sonokokotiyosa", "atasinokubiwosimeteirunda", "subete", "kaesitekurekaesitekure", "kaesitekuretohaiwanai", "sisouwokurauasamasisawo", "awaremidegomakasitesimaunara", "hikyoutoiwaretemosikatanai", "erezi-nizyounantewakanai", "sonomewotozitewaraouka", "end" },
            EndTime = new float[] {  18.1f, 21.9f, 25.6f, 29.5f, 30.1f, 33.2f, 35.1f, 37.0f, 37.7f, 40.8f, 44.3f, 44.8f, 46.6f, 48.3f, 50.1f, 52.3f, 53.9f, 55.9f, 57.2f, 59.4f, 59.7f, 61.4f, 63.3f, 65.2f, 67.3f, 69.2f, 70.7f, 74.9f }
* Chartreuse
            StartTime = new float[] { },
            Furigana = new string[] { },
            Mondai = new string[] { },
            romaji = new string[] { },
            EndTime = new float[] { }
* Psyched
            StartTime = new float[] {},
            Furigana = new string[] {},
            Mondai = new string[] {},
            romaji = new string[] {},
            EndTime = new float[] {}
* Dogbite   
            StartTime = new float[] {},
            Furigana = new string[] {},
            Mondai = new string[] {},
            romaji = new string[] {},
            EndTime = new float[] {}
* チュートリアル
  　　　　　StartTime = new float[] {1.379f ,2.976f,4.535f,6.101f,6.257f,6.797f,6.904f,7.039f,7.124f,7.253f,7.309f,7.372f,7.421f,7.483f,7.520f,7.575f,8.289f,9.856f,11.474f,12.625f,13.355f,14.088f,14.824f,15.559f,16.890f,18.225f,19.553f,20.872f,22.179f,27.741f,29.032f,30.416f,32.038f,33.370f,34.683f,37.106f,38.434f,39.767f,41.108f,42.436f,43.769f,48.373f,49.705f,51.053f,52.372f,53.700f,55.043f,56.369f,57.700f,59.955f,61.868f,63.205f,64.495f},
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "チュートリアル", "初めて遊ぶ方へのゲーム説明です", "それでは", "　", "Let's Go!!", " ", "Let's Go!!", " ", "Let's Go!!", " ", "Let's Go!!", "", "Let's Go!!", "", "Let's Go!!", "", "ノーツというオブジェクトが落ちてきます", "線と重なるときにキーを押してください", "タイミングによって", "Perfect", "Great", "Bad", "Miss", "四種類の判定がされます", "それぞれ落ちてくるレーンを見て", "正しいキーを押してください", "ASDF JKL;キー以外にも", "タイピングの指と同じキーが使えます", "　", "画面説明", "画面左上 スコアゲージです", "スコアによってバーが進みます", "MAXスコアは1000000", "フルスコアを目指してください", "　", "画面右上 メニューボタン", "ゲームのリタイア・リトライ", "テキストパネルの表示・非表示", "などが出来ますよ", "便利ですね", "　", "タイトル画面に戻って", "設定画面を開くと", "ゲームの設定を変更できます", "ノーツが落ちてくるタイミング", "ノーツの速さ", "レーン端のフラッシュのスピード", "フラッシュのタイミング", "もちろん音量調節も出来ます", "　", "これで説明はおしまい", "いろんな曲で遊んでみてください", "　" },
            romaji = new string[] {"", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            EndTime = new float[] {  2.9f, 4.5f, 6.09f, 6.2f, 6.7f, 6.9f, 7.0f, 7.1f, 7.2f, 7.3f, 7.3f, 7.4f, 7.4f, 7.5f, 7.5f, 8.2f, 9.8f, 11.4f, 12.6f, 13.3f, 14.0f, 14.8f, 15.5f, 16.8f, 18.2f, 19.5f, 20.8f, 22.1f, 27.7f, 29.0f, 30.4f, 32.0f, 33.3f, 34.6f, 37.1f, 38.4f, 39.7f, 41.1f, 42.4f, 43.7f, 48.3f, 49.69f, 51.0f, 52.3f, 53.69f, 55.0f, 56.3f, 57.69f, 59.9f, 61.8f, 63.19f, 64.4f }
               
* セルフィー
            StartTime = new float[] {0.951f,1.289f,3.323f ,4.298f,6.036f,8.285f,9.219f,11.086f,21.479f,23.906f,26.269f,28.936f,31.311f,32.497f,34.993f,37.568f,39.936f,42.859f,45.980f,47.942f,50.330f,51.547f,52.733f,53.879f,56.635f,59.175f,61.418f,62.616f,63.979f,66.471f,71.575f,72.200f},
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "あー", "機械仕掛け身に咲いた花", "掻いた荒", "元気に泣いたかな", "繰り返す日々に開いた穴", "吐いた金", "綺麗になったかな", "　", "頭から入れるただ酸素", "いつからか慣れてたスタント", "後ろから殴られ笑うの", "遠慮がちな若きクワント", "　", "階段を登る淡々と", "気をつけろと声が五万と", "黒く染まる為のコマンド", "結局押すマリオネット", "右向け右", "左向け左", "掛け声次第に", "体染みる", "擬態して", "期待して", "誰かが助けてくれるまで", "枯れた声を更に枯らして", "身体中の花を隠した", "擬態して", "期待して", "溢れたものをまた飲み込んで", "飲み込んで", "吐く", "　" },
            romaji = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            EndTime = new float[] { 1.2f, 3.3f, 4.2f, 6.0f, 8.2f, 9.2f, 11.0f, 21.4f, 23.89f, 26.2f, 28.9f, 31.3f, 32.4f, 34.9f, 37.5f, 29.9f, 42.8f, 45.9f, 47.9f, 50.3f, 51.5f, 52.7f, 53.8f, 56.6f, 59.1f, 61.4f, 62.6f, 63.9f, 66.4f, 71.5f, 72.19f }
        
* 4th smile
            StartTime = new float[] {},
            Furigana = new string[] {},
            Mondai = new string[] {},
            romaji = new string[] {},
            EndTime = new float[] {}
* パノプティコン
　　　　　　StartTime = new float[] { 46.174f, 47.684f, 49.008f, 50.308f, 51.650f, 53.113f, 54.509f, 55.706f, 57.317f, 58.605f, 59.958f, 61.318f, 62.639f, 64.151f, 65.486f, 66.760f, 68.956f, 74.315f, 79.997f, 85.479f, 89.900f, 91.704f, 92.396f, 93.291f, 95.805f, 100.816f, 102.671f, 103.476f, 104.286f, 106.806f, 112.451f },
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "S", "M", "T", "W", "T", "F", "S", "Back to the beginning .", "S", "M", "T", "W", "T", "F", "S", "Back to the beginning .", "ずっと 見ています", "アナタを見ています", "アナタのすべてが", "きっと 終わるまで", "期待なんてされてない", "できない", "いらない", "見ないフリしてそんじゃバイバイ", "「そんなことは思ってもないクセに！」", "'キライ'なんて限りない", "言えない", "in the night", "キライすぎて頭が痛い", "「そんなことは思ってもないクセに！」", " " },
            romaji = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            EndTime = new float[] { 47.600f, 49.990f, 50.290f, 51.600f, 53.013f, 54.409f, 55.606f, 53.290f, 58.590f, 59.920f, 61.300f, 62.600f, 64.100f, 65.400f, 66.700f, 68.900f, 74.300f, 79.970f, 85.430f, 89.870f, 91.650f, 92.330f, 93.260f, 95.780f, 100.800f, 102.630f, 103.430f, 104.240f, 106.780f }
* GURU      
            StartTime = new float[] { 0.971f, 1.339f, 1.503f, 2.004f, 2.569f, 2.869f, 3.439f, 3.809f, 4.009f, 4.339f, 4.709f, 4.979f, 5.409f, 6.079f, 6.279f, 6.509f, 6.709f, 6.909f, 7.309f, 21.809f, 22.680f, 22.924f, 24.302f, 25.256f, 26.343f, 28.914f, 30.245f, 31.962f, 32.470f, 33.639f, 35.008f, 35.762f, 38.509f, 39.298f, 42.060f, 42.815f, 44.311f, 46.288f, 47.513f, 49.783f, 54.321f, 56.187f, 56.414f, 58.987f, 60.692f, 62.505f, 64.315f, 66.044f, 68.738f, 70.611f },
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "あ", "わん", "だらね", "ばでぃん", "えん", "ばみん", "ぶらだ", "らだ", " ", "あ", "わん", "だらね", "ばでぃん", "お", "え", "お", "え", "お", " ", "SOS", "", "カスカスに乾いた体", "枯らしたenergy", "限界です", "ってな顔で 並んだ輩陰惨なmelody", "拝んだね願ったね", "平気そうなフリしてよう耐えたね", "あ", "お初にお目にかかります", "さぁ皆共よ傾聴", "颯", "我々は腐ってますまずまず軸がブレてます", "益々増す", "不安に白目ってます 且つ嘗てなくズレてます", "おうおうおう", "揺りかごから墓場 その先は", "pantry 果ては sewerage", "そして行き着く先がmelancholy", "もう祈っちゃいられんなGandhara", "(o-e-o)", "ほら旗立てろ", "あゝ", "SARUになってZUBUになっていく", "マトモは蹴飛ばして","キモいことしようぜ", "理屈も真っ新になって","GURUになっていく", "お前もBAKAされたんじゃねぇんですか？", "どうよ say hoo？", "", "", "", "", "" },
            romaji = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            EndTime = new float[] { 1.300f, 1.49f, 1.990f, 2.500f, 2.800f, 3.400f, 3.790f, 3.990f, 4.300f, 4.690f, 4.900f, 5.39f, 6.000f, 6.200f, 6.490f, 6.690f, 6.890f, 7.290f, 21.790f, 22.640f, 22.900f, 24.590f, 25.200f, 26.300f, 28.900f, 30.200f, 31.900f, 32.430f, 33.600f, 34.990f, 35.700f, 38.490f, 39.270f, 42.020f, 42.800f, 44.300f, 46.230f, 47.500f, 49.730f, 54.300f, 56.140f, 56.400f, 58.950f, 60.660f, 62.490f, 64.300f, 66.000f, 68.700f, 70.600f }
*阿吽のビーツ
　          StartTime = new float[] { 19.153f, 21.426f, 23.816f, 28.412f, 30.777f, 32.995f, 37.271f, 37.865f, 40.152f, 42.553f, 44.777f, 46.595f, 47.139f, 49.425f, 52.605f, 53.621f, 56.526f },
            Furigana = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            Mondai = new string[] { "みんなどっか行っちゃったよ", "零になっちゃってざまぁないね", "信じてたいから 声に出すのはやめた", "愛されたいのはどうして", "愛してたいのはどうして", "飾り合って 分かち合っていた", "私", "曖昧さ故にシンパシー", "大胆不敵なセンソリー", "どんまいどんまい大丈夫", "笑わせてあげるから", "だから", "与え与えられて", "消えないように此処に居なよ", "そしたら", "「僕にもお返事くださいね」", " " },
            romaji = new string[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
            EndTime = new float[] { }
*混沌ブギ
            StartTime = new float[] { 0.601f,1.814f,3.170f,4.085f,　　　　　　　　　　　　　　　　　　　　　　　　5.729f,      7.055f         ,8.213f                    ,10.754f        ,12.202f        ,13.995f                           ,15.478f,15.967f                   ,18.485f                    ,20.810f,30.935f      ,33.486f                 ,35.957f       ,38.362f         ,40.950f                         ,43.502f                     ,46.011f                       ,48.420f                    ,50.328f   ,51.310f                 ,53.712f                 ,56.322f                           ,61.312f         ,62.514f         ,63.816f       ,64.679f                    ,66.378f       ,67.561f       ,68.852f                    ,71.450f       ,72.646f       ,73.932f           ,74.977f                ,76.595f                   ,79.091f},
            Furigana = new string[] { },
            Mondai = new string[] { "純情？なにそれ","愛情？なにそれ","美味しいの？","ねぇ？美味しいのって なあ","乱暴ダメダメ","感情捨て去り","いっせーので ぶっ飛ぶだけ","混沌ブギウギ","ハッピー 特盛","ドンビーシャイだ ドンビーシャイだ","もう","段々堕ちてく IQ クソワロ","いっせーので 死ぬまで踊れ"," "   ,"完全体の山田","無関心なんてありえない","三千体の桑田","の方がいいかな","わがままばかりじゃ落ち着かない","冷や汗が出すぎて気持ち悪い","当たり前のことも手につかない","もう なんて言ったらいい？","ばかりで","世間体とか投げ出したい","狂った様に泣き出したい","思うがまま生きて消えていきたいな","純情？なにそれ","愛情？なにそれ","美味しいの？","ねぇ？美味しいのって なあ","乱暴ダメダメ","感情捨て去り","いっせーので ぶっ飛ぶだけ","混沌ブギウギ","ハッピー特盛","ドンビーシャイだ","ドンビーシャイだ もう","段々堕ちてく IQ クソワロ","いっせーので 死ぬまで踊れ"},
            romaji = new string[] { },
            EndTime = new float[] { }
*春嵐
            StartTime = new float[] {15.427f                           ,18.401f                         ,20.546f           ,22.259f           ,23.490f            ,25.298f                     ,27.424f                     ,29.348f         ,31.168f            ,32.779f           ,34.391f         ,36.075f       ,37.412f     ,38.191f         ,39.421f                         ,41.298f                          ,42.877f          ,44.129f    ,45.113f             ,46.321f               ,47.969f       ,48.767f     ,49.798f                                   ,53.221f},
            Furigana = new string[] { },
            Mondai = new string[] { "虚像に塗れた私 まるで神様みたいね","本当の気持ちとか知ったかぶりで","君が悪いの妖共め","何かを掴んだとて","それで幻想 壊して","想いを冷ますとか具の骨頂ね","ただの雑魚に取り合わないの","冷静 装う僕が","言葉を口にするのは","簡単に虎視眈々と","怒っているから","春の嵐呼んだ","僕は泣いた","心傷つけられた","「そんなの思い上がりでしょ？」","どうせそうよね 分かっているけど","吐いた息もきっと","煙たくて","目障りに映るでしょ","愛されたいと願うのは","罪というのね","甘い香りで","揺れる炭酸 飲み干したら サヨナラしましょ"," "},
            romaji = new string[] { },
            EndTime = new float[] { }
*ビビビビ
            StartTime = new float[] { 6.341f, 8.869f, 12.016f, 14.290f, 17.344f, 20.215f, 22.859f, 25.218f, 28.437f, 30.281f, 33.825f, 36.269f, 39.424f, 40.101f, 40.756f, 41.488f, 42.002f, 44.667f, 47.271f, 50.377f, 50.774f, 53.288f, 55.833f, 59.979f ,61.337f,71.781f},
            Furigana = new string[] { },
            Mondai = new string[] { "ぼくらの生まれたときから", "とっくに地図は埋まりきっていて", "敷き詰められてる建物", "避けるみたいに街を歩いてる", "いつの間にかこのゲームの上", "何かを求め足掻いてる", "中身空っぽのマクガフィン", "知らない誰かの夢を追ってる", "(溜息) ", "じゃあルールに則る以外のやり方、教えてよ", "末期決まってる退屈", "スペルミスすら今は愛おしい", "コピーの", "コピーのコピーの", "コピーのコピーのコピーの", "コピーのコピーのコピーのコピー", "みたいな生活なげうって", "教室こっそり抜け出して", "届きそうなサイン送ってみたくて", " ", "マークシートに書いたのは", "不正解のアレシボ・メッセージ", "そしたら空からそれは来た", " ","息も出来ないくらいの衝撃","それは衝撃を割って割いたエイリアン" },
            romaji = new string[] { },
            EndTime = new float[] { }
*灰Φ倶楽部
            StartTime = new float[] { 7.647f    ,9.403f        ,11.019f           ,14.296f             ,16.089f           ,17.867f                          ,20.958f            ,22.143f,22.891f            ,23.738f          ,25.114f                   ,27.604f            ,29.012f,29.423f            ,30.537f                 ,32.689f,33.220f,33.653f,33.879f,34.495f       ,35.584f,36.725f         ,38.167f           ,39.400f           ,40.868f                           ,43.954f                         ,47.307f ,47.935f       ,49.042f,50.116f     ,51.631f             ,52.823f             ,54.167f                   ,57.476f ,57.873f                       ,59.612f         ,67.910f},
            Furigana = new string[] { "満月の夜","案山子が招く","探し物は扉の奥さ","バンケットルームは","若さで賑わってる","盗みに入った様な 居心地にて待つ","アヴァラ マダラダ"," "    ,"アヴァラ マダラダ","ほら 旧世界さん","新世界の美酒も悪くないね","アヴァラ マダラダ",""     ,"アヴァラ マダラダ","アホロートルと化してく","V."   ,"V.I.","V.I.P","そうさ","ネ申ネ申ネ申","など" ,"在りはしないと","もう解ってるんだ","解ってるんだって","「喜び無き日々からおさらばです」","意味のない感情の逝き場は何処？","そうさ","ネ申ネ申ネ申","でも" ,"頼らないと","もうやってらんない","やってらんないって","命の仕組みからさ逸脱しても","いつか","ブラッティでブラックシープな","欲の向こう側へ",""},
            Mondai = new string[] { "満月の夜", "案山子が招く", "探し物は扉の奥さ", "バンケットルームは", "若さで賑わってる", "盗みに入った様な 居心地にて待つ", "アヴァラ マダラダ", " ", "アヴァラ マダラダ", "ほら 旧世界さん", "新世界の美酒も悪くないね", "アヴァラ マダラダ", "", "アヴァラ マダラダ", "アホロートルと化してく", "V.", "V.I.", "V.I.P", "そうさ", "ネ申ネ申ネ申", "など", "在りはしないと", "もう解ってるんだ", "解ってるんだって", "「喜び無き日々からおさらばです」", "意味のない感情の逝き場は何処？", "そうさ", "ネ申ネ申ネ申", "でも", "頼らないと", "もうやってらんない", "やってらんないって", "命の仕組みからさ逸脱しても", "いつか", "ブラッティでブラックシープな", "欲の向こう側へ", "" },
            romaji = new string[] { },
            EndTime = new float[] { }
*オーバーライド
            StartTime = new float[] {12.086f ,14.485f                                          ,16.357f                             ,19.661f             ,21.497f               ,23.401f                                 ,24.728f             ,26.761f             ,28.384f   ,29.471f                ,31.413f                            ,35.587f                   ,37.771f     ,39.137f     ,40.159f                 ,42.489f                     ,45.209f             ,47.362f                     ,49.660f                 ,51.918f                         ,54.455f                     ,57.245f       ,58.477f    ,59.298f},
            Furigana = new string[] { },
            Mondai = new string[] { "バッドランドに生まれただけで"　,"バッドライフがデフォとか","くだらないけど、それが理なんだって","もう参っちゃうね。","抗うためにエスケープ","抗うためにエスケープ・フロム・デンエン","蛇のように這い善戦","だけど最後、逆転の","一手だけ","何故か詰められないの!","暗い無頓社会vsBRIGHT未来世界 なら","ちょっと後者に行ってくる","大丈夫か？","うるせえよ","限界まで足掻いた人生は","想像よりも狂っているらしい","半端な生命の関数を","少々ここらでオーバーライド","…したい所だけど現実は","そうそう上手くはいかないようで","吐いた言葉だけが信条だって","思われてまた","離れ離れ",""},
            romaji = new string[] { },
            EndTime = new float[] { }
*イガク
            StartTime = new float[] { 12.742f,13.106f            ,15.967f         ,18.746f      ,21.489f    ,22.392f  ,24.420f              ,27.311f       ,30.175f        ,32.785f ,33.763f,35.324f,35.652f                   ,38.566f                         ,41.338f,42.487f,43.857f,44.703f,45.308f            ,46.642f,47.024f                    ,49.777f                 ,52.251f,52.698f                ,55.091f   ,55.477f             ,56.528f                     ,58.281f                     ,61.086f                 ,63.542f,63.959f            ,66.396f   ,66.769f         ,67.767f                 ,69.212f   ,69.626f},
            Furigana = new string[] { },
            Mondai = new string[] {"ユ！","ドクター・キドリです","愛・爆破ッテロ","簡単になれば","埋まった" ,"マター","ドクター・キドリです","愛想良いかも","朦朧 オオトも","埋めた","メタ","ユ！","何処にも無いから寝ていたら","壊れて泣いてるユメを診たんだよ","     ","次期","には","嘘"      ,"嘘に診えてクルゥ","ユ！","カオが→鈍器になっちゃうヨ","偽が→権利を取ったんだ","ユ！","無くなってほしい煩悩が","ドウヤラ","生き延びてしまった","生き延びてしまったんだ！！","足が→タガメになっちゃうヨ","嘘が→動機になったんだ","ユ！","疑ってほしい本能を","ドウヤラ","本心だと思った","本心だと思ったんだ！！","クチョォ",""},
            romaji = new string[] { },
            EndTime = new float[] { }
*テレキャスタービーボーイ
            StartTime = new float[] { 0.587f,0.901f,11.317f       ,13.932f                 ,16.509f           ,19.188f           ,21.946f             ,24.621f               ,27.122f               ,29.665f           ,32.503f             ,35.136f             ,37.697f                    ,39.181f    ,40.558f                   ,42.835f                   ,44.659f             ,45.957f             ,48.373f             ,50.779f                 ,53.465f                   ,55.116f      ,56.394f              ,58.944f},
            Furigana = new string[] { },
            Mondai = new string[] { "ま","","大人になるほどDeDeDe","はみ出しものです伽藍堂","人生論者が語った","少女は鳥になって","綺麗事だけでPaPaPa","ボロボロの靴を結んで","デジタル信者が祟った","少年は風になって","ゆらりくらり大往生","お疲れ様ですご臨終","テレキャスタービーボーイ","僕に愛情を","嘘で固めたウォーアイニー","うざったいんだジーガール","魅惑ハイテンション","カニバリズム踊れば","一つ二つ殺めた手で","何を描いているんだろう","テレキャスタービーボーイ","僕に愛情を","誰か答えてくれないか",""},
            romaji = new string[] { },
            EndTime = new float[] { }
*一龠
            StartTime = new float[] { 1.580f                ,4.297f              ,7.027f          ,8.781f          ,11.319f      ,12.307f,12.965f,13.448f,13.922f,14.424f,15.300f   ,16.160f          ,18.123f ,18.779f   ,19.555f,20.086f,20.923f         ,22.364f     ,23.543f,23.704f,24.005f,24.389f,24.650f,25.064f,25.243f,25.571f,25.796f,26.152f,26.461f,26.783f,26.956f,27.322f,27.765f,28.002f,28.303f,28.608f,28.752f,29.041f,29.258f,29.371f,29.538f,30.035f,30.594f,30.738f,30.952f,31.267f,31.522f,31.773f,31.940f,32.339f,32.610f,32.807f,33.068f,33.417f,34.234f,34.644f,35.011f,35.403f  ,36.091f,36.701f   ,37.906f  ,38.591f          ,40.082f           ,41.771f             ,43.449f ,44.230f,45.110f    ,45.925f,46.487f  ,47.178f   ,48.215f ,48.888f,49.213f,49.920f         ,51.243f,71.200f     ,71.929f,72.377f,72.872f,73.217f     ,74.460f  ,75.312f        ,77.131f         },
            Furigana = new string[] { },
            Mondai = new string[] {"一つひかるもんがあって" ,"二つひとがつないで","三つおってゆく","ひとおってゆく","ひを奏でろ","実なる","宴に","委ね"  ,"種が","稲穂へ","恵まれた","念はされど0.5合","口では","嘆ずるが","詞に","妬んで","唇を噛んでいる","惑う思いは","戦意","減産"  ,"流星" ,"育児" ,"前祝" ,"帰社","帝室"  ,"暗転","爆撃"  ,"愁殺"  ,"透析","遮断","墨継"  ,"圧縮","仮寝"   ,"荷受","歌謡","欺計"  ,"旗竿"  ,"放管","教師","孤帆","学帽"   ,"蛍草","強膜"  ,"弾奏","果実"  ,"規定","真跡","異変"    ,"惜愛","懐想","袋棚"  ,"武骨","御鍋","待針"    ,"挫け","腐り","薄る"  ,"花信を","誇り","朽ちないで","殺げる","柄じゃないじゃん","再起とかしないで","飽きじゃ飼われんぜ","口説け","詠え"  ,"さあ永を","泳ぎ","汚される","巧として","貢ぐは","負と","危ぶむ","厄になってやる",""     ,"さあ、さあ","願う","類の","お題で","影が憬れて","悦を競い","たったの0.5合","捻っては揺らぎ"},
            romaji = new string[] { },
            EndTime = new float[] { }

 */

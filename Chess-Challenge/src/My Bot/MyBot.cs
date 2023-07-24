using ChessChallenge.API;
using System;
using System.Linq;

// ReSharper disable All

public class MyBot : IChessBot
{

    /*
    ulong[] packedWeights = {
        // Piece Ranks Mid -- [0, 6)
        8319120971697340275, 5874251426396084280, 9115708401635983471, 8530495463693916074, 9617577176891814286, 9548307828211688616,

        // Piece Ranks End -- [6, 12)
        7740960348599077739, 7887070210182904695, 8392317741637468544, 8537552219598456704, 5720820050425717139, 7169037431592683624,

        // Piece Files Mid -- [12, 18)
        9193406494467062129, 6885879089610721364, 9836561401613549700, 9259822503084325754, 9979836567201609855, 10200475517429977216,

        // Piece Files End -- [18, 24)
        8752880576781910663, 8177276395744295280, 8826916209434852986, 8394569528499274619, 9767050345308649574, 8035993554067947886,

        // Centrality Mid -- [24, 30)
        67555068169158932, 87821176297160942, 63614409905471776, 67555136888766722, 69806975356567802, 68962305615790379,
        
        // Centrality End -- [30, 36)
        74592032781107432, 70651340157812987, 70088373024653566, 75154883950215422, 69243849310667027, 75154914014396664
    };
    */

    int[] weights = { 0, 107, 424, 464, 596, 1256, 0, 0, 185, 421, 466, 813, 1480, 0, 0, 0, 1, 1, 2, 4, 0 };

    ulong[] PST = new ulong[]{
        7957419012188434030, 8110565592985792865, 8397939501287371363, 7963062874328626276, 7891583662196161128, 8324518672300276582, 8468426212635174347, 7957419012188434030,
        6373283344978833709, 9113456597441866342, 8613283303442971249, 9193128386997288057, 10416162467122219651, 11579823103930177130, 9330809297772505975, 3850078221741212163,
        8100413776424367999, 9407030600038449785, 9693015786465100156, 9692170343762068344, 9044804290237465462, 9555692406588804473, 8037977059672164976, 9544910315414714717,
        7740119234984766057, 4933252940294351187, 7456105459987342681, 8321371658944995424, 9041130744243713136, 11076795253885273715, 11716040379556727181, 14018206686210924702,
        7240219693812182650, 8034564111121545324, 8683918056217804400, 8972432114925925747, 9332170325006512244, 10643895445131982706, 10560844882181973112, 11287928020776414316,
        10705436158055719051, 10631982051071459462, 8250421216894873719, 5940073517116727906, 5007577701860015738, 8196479490762191515, 3489620850413117927, 8981410384831384454,
        6582955728264977243, 7453583245040908155, 7597128855893866099, 7957694968064931706, 8897263950836105607, 12369293145507674296, 17719347603895410663, 6582955728264977243,
        7810218637687151993, 7311165767527922539, 8321954555365851504, 9119679962693732733, 9409316639920001406, 7744372245368374137, 7889310963205964913, 4711470475929744481,
        8177819472580278122, 7527616691673330549, 8393732847479325047, 8395143503835661693, 9551437236290751359, 9621247359757484674, 8826627029253718141, 8176988284858432377,
        7745761993352708214, 9327366576036279930, 8247913351787347321, 8609612017005136003, 9187767133873407365, 8679980752542533001, 9115698527589665928, 9045338566620120710,
        6074886815076801627, 7517444971827913570, 9040275298142216046, 10198562531715681390, 11935563407808432238, 11712333951627984501, 10347188011096838516, 9548327774458125437,
        5721957894282109001, 7527352864952579952, 8611321830836175731, 9190886534647282291, 9986056638044607361, 9986335832158539142, 10275685498043665507, 8105789387337519945
    };

    struct TT_Entry
    {
        public ulong key;
        public Move move;

        public TT_Entry(ulong new_key, Move new_move)
        {
            key = new_key;
            move = new_move;
        }
    };

    const ulong ttLength = 1048576;
    TT_Entry[] transpositionTable = new TT_Entry[ttLength];

    ulong[] repetitionTable = new ulong[1024];

    Move bestMoveRoot = Move.NullMove;

    Move[,] killers = new Move[128, 3];
    //int[,,] history = new int[2, 7, 64];

    public int gamePly = 0, nodes = 0;

    bool stopped = false;

    int Evaluate(Board board)
    {
        int[] scores = {0, 0};
        int gamePhase = 0;

        for (int color = 0; color < 2; color++)
        {
            for (int pieceType = 1; pieceType <= 6; pieceType++)
            {
                ulong pieceBB = board.GetPieceBitboard((PieceType)pieceType, color == 0);

                while (pieceBB != 0)
                {
                    int square = BitboardHelper.ClearAndGetIndexOfLSB(ref pieceBB) ^ 56 * color;

                    for (int phase = 0; phase < 2; phase++)
                        scores[phase] +=
                            weights[7 * phase + pieceType] +
                            2 * (int)(PST[8 * pieceType - 8 + 48 * phase + square / 8] >> square % 8 * 8 & 255) - 256;  // Subtract by 2 * 128
                    

                    gamePhase += weights[14 + pieceType];
                }
                
            }

            scores[0] *= -1;
            scores[1] *= -1;
        }

        // Tempo + Tapered Eval * side
        return 25 + (scores[0] * gamePhase + scores[1] * (24 - gamePhase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
    }

    public Move Think(Board board, Timer timer)
    {
        repetitionTable[++gamePly] = board.ZobristKey;

        nodes = 0;
        stopped = false;

        for (int depth = 1; depth < 64; depth++)
        {
            // 83 tokens saved here

            //Negamax(board, timer, -1000000, 1000000, depth, 0);

            int returnEval = Negamax(board, timer, -1000000, 1000000, depth, 0);

            if (timer.MillisecondsElapsedThisTurn * 40 >= timer.MillisecondsRemaining) break;

            Console.WriteLine("info depth " + depth.ToString() + " score cp " + returnEval.ToString() + " nodes " + nodes.ToString() + " nps " + (nodes / Math.Max(timer.MillisecondsElapsedThisTurn, 1) * 1000).ToString());
        }

        board.MakeMove(bestMoveRoot);
        repetitionTable[++gamePly] = board.ZobristKey;

        return bestMoveRoot;
    }

    public int Negamax(Board board, Timer timer, int alpha, int beta, int depth, int ply)
    {
        if ((nodes & 2047) == 0 && timer.MillisecondsElapsedThisTurn * 8 >= timer.MillisecondsRemaining) stopped = true;

        TT_Entry ttEntry = transpositionTable[board.ZobristKey % ttLength];

        bool qsearch = depth <= 0, inCheck = board.IsInCheck(), pvNode = alpha != beta - 1;

        var moves = board.GetLegalMoves(qsearch);


        repetitionTable[ply + gamePly] = board.ZobristKey;

        // Check Extensions
        // 15.2 +- 13.8 .... 5 Tokens
        if (inCheck) depth++;

        if (qsearch)
        {
            int staticEval = Evaluate(board);
            if (staticEval >= beta) return staticEval;
            if (staticEval > alpha) alpha = staticEval;
        }
        else if (ply > 0) {

            if (moves.Length == 0) return inCheck ? ply - 900000 : 0;

            for (int i = gamePly + ply - 2; i >= 0; i--)
                if (repetitionTable[i] == board.ZobristKey) return 0;

            // RFP
            // 47.1 +/- 20.3 .... 28 Tokens
            if (!pvNode && !inCheck && depth <= 6)
            {
                int staticEval = Evaluate(board);
                if (staticEval - 150 * depth >= beta) return staticEval;
            }
        }

        var scores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            int score = 0;
            if (ttEntry.key == board.ZobristKey && move == ttEntry.move) score += 500000;
            if (move.IsCapture)
                score += 50000 + 5 * weights[(int)move.CapturePieceType] -
                    weights[(int)move.MovePieceType];
            else
            {
                if (move == killers[ply, 0]) score += 20000;
                if (move == killers[ply, 1]) score += 15000;
                if (move == killers[ply, 2]) score += 10000;
                //score += history[board.IsWhiteToMove ? 0 : 1, (int)move.MovePieceType, move.TargetSquare.Index];
            }
            scores[i] = score;
        }

        Move bestMove = Move.NullMove;
        for (int currentCount = 0; currentCount < moves.Length; currentCount++)
        {

            int bestIndex = currentCount, newAlpha = -beta, newDepth = depth - 1, returnScore;

            for (int nextCount = currentCount + 1; nextCount < moves.Length; nextCount++)
                if (scores[nextCount] > scores[bestIndex]) bestIndex = nextCount;

            (scores[bestIndex], scores[currentCount]) = (scores[currentCount], scores[bestIndex]);

            Move move = moves[bestIndex];
            moves[bestIndex] = moves[currentCount];
            moves[currentCount] = move;


            board.MakeMove(move);

            nodes++;

            // PVS
            if (currentCount == 0 || qsearch) goto Search;

            newAlpha = -alpha - 1;

            // Late Move Reductions
            if (currentCount >= 3 && depth >= 3 && !move.IsCapture && !inCheck)
            {
                newDepth -= 1 + depth / 10 + currentCount / 12;
                if (pvNode) newDepth++;
            }

            Search:
                returnScore = -Negamax(board, timer, newAlpha, -alpha, newDepth, ply + 1);

            // Researches
            if (newDepth != depth - 1 && returnScore > alpha)
            {
                newDepth = depth - 1;
                goto Search;
            }

            if (newAlpha == -alpha - 1 && pvNode && returnScore > alpha && returnScore < beta)
            {
                newAlpha = -beta;
                goto Search;
            }
            //

            board.UndoMove(move);

            if (stopped) return 0;

            if (returnScore > alpha)
            {
                alpha = returnScore;
                bestMove = move;
                if (ply == 0) bestMoveRoot = move;

                if (returnScore >= beta)
                {
                    if (!move.IsCapture)
                    {
                        killers[ply, 2] = killers[ply, 1];
                        killers[ply, 1] = killers[ply, 0];
                        killers[ply, 0] = move;
                    }
                        //history[board.IsWhiteToMove ? 0 : 1, (int)move.MovePieceType, move.TargetSquare.Index] =
                        //    history[board.IsWhiteToMove ? 0 : 1, (int)move.MovePieceType, move.TargetSquare.Index] * depth * depth / 324 + depth * depth * 32;
                    
                    break;
                    
                }
            }
        }

        transpositionTable[board.ZobristKey % ttLength] = new TT_Entry(board.ZobristKey, bestMove);

        return alpha;
    }

}
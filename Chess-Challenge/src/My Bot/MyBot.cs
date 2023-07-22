using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{

    int[] pieceOrderingValues = { 0, 100, 300, 350, 600, 1100, 0};
    int[] gamePhases = { 0, 0, 1, 1, 2, 4, 0 };

    int[] pieceValuesMid = { 0, 100, 300, 320, 600, 1100, 0 };
    int[] pieceValuesEnd = { 0, 120, 300, 320, 600, 1100, 0 };

    private Move bestMove = Move.NullMove;
    private int nodes = 0;
    private int hardTimeLimit = 60000;
    private int softTimeLimit = 60000;

    private bool stopped = false;

    private int Evaluation(Board board)
    {
        int[] scoresMid = {0, 0};
        int[] scoresEnd = { 0, 0 };
        int gamePhase = 0;

        for (int color = 0; color < 2; color++)
        {
            for (int pieceType = 0; pieceType < 6; pieceType++)
            {
                ulong pieceBB = board.GetPieceBitboard((PieceType)pieceType, color == 0);

                int pieceCount = BitboardHelper.GetNumberOfSetBits(pieceBB);

                scoresMid[color] += pieceCount * pieceValuesMid[pieceType];
                scoresEnd[color] += pieceCount * pieceValuesEnd[pieceType];
                gamePhase += pieceCount * gamePhases[pieceType];

                /*
                while (pieceBB != 0)
                {
                    BitboardHelper.ClearAndGetIndexOfLSB(ref pieceBB);
                }
                */
            }
        }

        if (gamePhase > 24) gamePhase = 24;

        int scoreMid = scoresMid[0] - scoresMid[1];
        int scoreEnd = scoresMid[0] - scoresMid[1];
        int evaluation = (scoreMid * gamePhase + scoreEnd * (24 - gamePhase)) / 24;

        return board.IsWhiteToMove ? evaluation : -evaluation;
    }

    public Move Think(Board board, Timer timer)
    {
        hardTimeLimit = timer.MillisecondsRemaining / 12;
        softTimeLimit = timer.MillisecondsRemaining / 41;

        Move realBestMove = bestMove;
        for (int depth = 1; depth < 64; depth++)
        {
            int returnEval = Negamax(board, timer, -1000000, 1000000, depth, 0);
            if (!stopped) realBestMove = bestMove;

            if (timer.MillisecondsElapsedThisTurn >= softTimeLimit) break;

            Console.WriteLine("info depth " + depth.ToString() + " score cp " + returnEval.ToString());
        }

        return realBestMove;
    }

    public int Negamax(Board board, Timer timer, int alpha, int beta, int depth, int ply)
    {

        if ((nodes & 2047) == 0 && timer.MillisecondsElapsedThisTurn >= hardTimeLimit) stopped = true;

        bool qsearch = depth <= 0;

        Move[] moves = board.GetLegalMoves(qsearch);
        bool inCheck = board.IsInCheck();
        bool pvNode = alpha != beta - 1;

        if (qsearch)
        {
            int staticEval = Evaluation(board);
            if (staticEval >= beta) return staticEval;
            if (staticEval > alpha) alpha = staticEval;
        }
        else if (moves.Length == 0)
        {
            return inCheck ? -900000 + ply : 0;
        }

        int[] scores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            int score = 0;
            if (move.IsCapture)
            {
                score += 50000;
                score += 5 * pieceOrderingValues[(int)move.CapturePieceType] -
                             pieceOrderingValues[(int)move.MovePieceType];
            }

            scores[i] = score;
        }

        for (int currentCount = 0; currentCount < moves.Length; currentCount++)
        {

            int bestIndex = currentCount;

            for (int nextCount = currentCount + 1; nextCount < moves.Length; nextCount++)
            {
                if (scores[nextCount] > scores[bestIndex])
                {
                    bestIndex = nextCount;
                }
            }

            Move move = moves[bestIndex];

            board.MakeMove(move);

            nodes++;

            int newAlpha = -beta;
            int newDepth = depth - 1;

            int returnScore;
            if (currentCount == 0) goto Search;
            else goto Window;

            Window:
                newAlpha = -alpha - 1;

            Search:
                returnScore = -Negamax(board, timer, newAlpha, -alpha, newDepth, ply + 1);

            if (newAlpha == -alpha - 1 && pvNode && returnScore > alpha && returnScore < beta)
            {
                newAlpha = -beta;
                goto Search;
            }

            board.UndoMove(move);

            if (stopped) return 0;

            if (returnScore > alpha)
            {
                alpha = returnScore;
                if (ply == 0) bestMove = move;

                if (returnScore >= beta) return beta;
            }

        }

        return alpha;
    }

}
namespace Riftward.App.Bench;

/// <summary>
/// Deterministisches 48-Bone-Graybox-Rig des Belastungsframes (T-023):
/// feste Knochenhierarchie, Bind-Pose-Inversen und prozedurale Gehpose.
/// Alle Werte entstehen aus Tickzeit und Einheitenindex per Doppelgenauigkeits-
/// Mathematik ohne Uhr-, Zufalls- oder Heapbeitrag im Auswertungspfad
/// (Auswerterinstanzen tragen ihren Arbeitsplatz im Konstruktor).
/// </summary>
public static class RepresentativeRig
{
    /// <summary>Anzahl Knochen je normaler sichtbarer Einheit (Szenebudget: 48).</summary>
    public const int BoneCount = RepresentativeScenario.BonesPerNormalUnit;

    /// <summary>Wurzelhoehe ueber Grund in Metern.</summary>
    public const double RootHeightMeters = 0.95;

    private static readonly int[] Parent =
    [
        -1,
        /* spine */      0, 1, 2, 3, 4, 5,
        /* neck/head */  6, 7,
        /* arm l */      6, 9, 10, 11,
        /* arm r */      6, 13, 14, 15,
        /* leg l */      0, 17, 18, 19, 20,
        /* leg r */      0, 22, 23, 24, 25,
        /* tail */       0, 27, 28, 29, 30,
        /* pack */       6, 6,
        /* antenna */    8, 8,
        /* jaw */        8, 8,
        /* brow */       8, 8,
        /* kneepad */    19, 24,
        /* elbow */      11, 15,
        /* heel */       20, 25,
        /* mantle */     6, 6,
    ];

    private static readonly double[] OffsetX =
    {
        0.0,
        0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        0.0, 0.0,
        -0.14, -0.16, -0.18, -0.14,
        0.14, 0.16, 0.18, 0.14,
        -0.09, 0.0, 0.0, 0.0, 0.0,
        0.09, 0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0, 0.0,
        -0.10, 0.10,
        -0.04, 0.04,
        -0.03, 0.03,
        -0.03, 0.03,
        0.0, 0.0,
        0.0, 0.0,
        0.0, 0.0,
        -0.17, 0.17,
    };

    private static readonly double[] OffsetY =
    {
        RootHeightMeters,
        0.10, 0.10, 0.10, 0.10, 0.10, 0.10,
        0.10, 0.10,
        0.06, 0.0, 0.0, 0.0,
        0.06, 0.0, 0.0, 0.0,
        -0.05, -0.22, -0.24, -0.26, -0.02,
        -0.05, -0.22, -0.24, -0.26, -0.02,
        -0.05, 0.0, 0.0, 0.0, 0.0,
        -0.02, -0.02,
        0.07, 0.07,
        -0.03, -0.03,
        0.03, 0.03,
        0.0, 0.0,
        0.0, 0.0,
        0.0, 0.0,
        -0.10, -0.10,
    };

    private static readonly double[] OffsetZ =
    {
        0.0,
        0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        0.0, 0.0,
        0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0, 0.10,
        0.0, 0.0, 0.0, 0.0, 0.10,
        -0.08, -0.10, -0.10, -0.10, -0.10,
        -0.09, -0.09,
        0.0, 0.0,
        0.05, 0.05,
        0.06, 0.06,
        0.04, 0.04,
        -0.04, -0.04,
        -0.05, -0.05,
        -0.05, -0.05,
    };

    /* Rotationsachse je Knochen: 0 == X (Nick), 1 == Y (Dreh), 2 == Z (Schwank). */
    private static readonly int[] Axis =
    {
        2,
        0, 0, 0, 0, 0, 2,
        0, 0,
        2, 0, 0, 0,
        2, 0, 0, 0,
        2, 0, 0, 0, 0,
        2, 0, 0, 0, 0,
        1, 1, 1, 1, 1,
        2, 2,
        2, 2,
        0, 0,
        0, 0,
        0, 0,
        0, 0,
        0, 0,
        2, 2,
    };

    /* Amplitude (Radiant), Frequenzmultiplikator und Phasenoffset je Knochen. */
    private static readonly double[] Amplitude =
    {
        0.00,
        0.02, 0.03, 0.03, 0.03, 0.03, 0.04,
        0.02, 0.03,
        0.02, 0.45, 0.35, 0.20,
        0.02, 0.45, 0.35, 0.20,
        0.01, 0.50, 0.55, 0.30, 0.18,
        0.01, 0.50, 0.55, 0.30, 0.18,
        0.12, 0.14, 0.16, 0.18, 0.20,
        0.03, 0.03,
        0.08, 0.08,
        0.06, 0.06,
        0.05, 0.05,
        0.10, 0.10,
        0.08, 0.08,
        0.06, 0.06,
        0.05, 0.05,
    };

    private static readonly double[] PhaseOffset =
    {
        0.0,
        0.0, 0.4, 0.8, 1.2, 1.6, 2.0,
        2.4, 2.8,
        0.0, 0.0, 0.9, 1.4,
        Math.PI, Math.PI, Math.PI + 0.9, Math.PI + 1.4,
        0.0, 0.0, 0.7, 1.2, 1.6,
        Math.PI, Math.PI, Math.PI + 0.7, Math.PI + 1.2, Math.PI + 1.6,
        0.3, 0.9, 1.5, 2.1, 2.7,
        1.1, 2.3,
        0.7, 2.9,
        0.4, 2.2,
        1.8, 3.0,
        0.9, 2.6,
        1.3, 2.8,
        0.6, 2.4,
        3.2, 3.6,
    };

    /// <summary>Gehfrequenz in Radiant je Simulationstick bei Normalschritt.</summary>
    public const double WalkPhasePerTick = 0.22;

    /// <summary>Weltpositionen der Ruhepose (Bind-Pose) je Knochen.</summary>
    public static (double X, double Y, double Z)[] BindPositions()
    {
        var positions = new (double, double, double)[BoneCount];

        for (var bone = 0; bone < BoneCount; bone++)
        {
            if (Parent[bone] < 0)
            {
                positions[bone] = (OffsetX[bone], OffsetY[bone], OffsetZ[bone]);
                continue;
            }

            var parent = positions[Parent[bone]];
            positions[bone] = (
                parent.Item1 + OffsetX[bone],
                parent.Item2 + OffsetY[bone],
                parent.Item3 + OffsetZ[bone]);
        }

        return positions;
    }

    public static int ParentOf(int bone) => Parent[bone];

    /// <summary>
    /// Auswerter einer Einheitenpose: traegt alle Zwischenmatrizen im
    /// Instanzfeld (keine Allokation je Aufruf) und schreibt die Palette
    /// als RGBA32F-Texelzeile: je Knochen drei Spaltentexel der affinen
    /// Weltmatrix (Spalten 0 bis 2; Spalte 3 ist implizit (0,0,0,1)).
    /// Hautmatrix je Knochen: Welt(bone, Pose) * BindInverse(bone).
    /// </summary>
    public sealed class PoseEvaluator
    {
        private readonly double[] _worldRotation;
        private readonly double[] _worldTranslation;
        private readonly double[] _bindInverseRotation;
        private readonly double[] _bindInverseTranslation;
        private readonly double[] _scratchRotation;
        private readonly double[] _scratchTranslation;
        private readonly double[] _jointRotation;
        private static readonly double[] ZeroTranslation = [0.0, 0.0, 0.0];

        public PoseEvaluator()
        {
            _worldRotation = new double[BoneCount * 9];
            _worldTranslation = new double[BoneCount * 3];
            _bindInverseRotation = new double[BoneCount * 9];
            _bindInverseTranslation = new double[BoneCount * 3];
            _scratchRotation = new double[BoneCount * 9];
            _scratchTranslation = new double[BoneCount * 3];
            _jointRotation = new double[9];

            ComputeBind();
        }

        private static void SetIdentity(double[] rotation, double[] translation, int bone)
        {
            var baseIndex = bone * 9;
            rotation[baseIndex + 0] = 1.0;
            rotation[baseIndex + 1] = 0.0;
            rotation[baseIndex + 2] = 0.0;
            rotation[baseIndex + 3] = 0.0;
            rotation[baseIndex + 4] = 1.0;
            rotation[baseIndex + 5] = 0.0;
            rotation[baseIndex + 6] = 0.0;
            rotation[baseIndex + 7] = 0.0;
            rotation[baseIndex + 8] = 1.0;

            translation[bone * 3 + 0] = 0.0;
            translation[bone * 3 + 1] = 0.0;
            translation[bone * 3 + 2] = 0.0;
        }

        private static void Multiply(
            double[] outR, double[] outT, int outBone,
            double[] aR, double[] aT, int aBone,
            double[] bR, double[] bT, int bBone)
        {
            var ai = aBone * 9;
            var bi = bBone * 9;
            var oi = outBone * 9;

            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var value =
                        (aR[ai + (row * 3) + 0] * bR[bi + (0 * 3) + column])
                        + (aR[ai + (row * 3) + 1] * bR[bi + (1 * 3) + column])
                        + (aR[ai + (row * 3) + 2] * bR[bi + (2 * 3) + column]);

                    outR[oi + (row * 3) + column] = value;
                }

                var tx = aR[ai + (row * 3) + 0] * bT[(bBone * 3) + 0]
                    + aR[ai + (row * 3) + 1] * bT[(bBone * 3) + 1]
                    + aR[ai + (row * 3) + 2] * bT[(bBone * 3) + 2]
                    + aT[(aBone * 3) + row];

                outT[outBone * 3 + row] = tx;
            }
        }

        private static void RotationMatrix(int axis, double angle, double[] target, int bone)
        {
            var i = bone * 9;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            switch (axis)
            {
                case 0:
                    target[i + 0] = 1.0;
                    target[i + 1] = 0.0;
                    target[i + 2] = 0.0;
                    target[i + 3] = 0.0;
                    target[i + 4] = cos;
                    target[i + 5] = -sin;
                    target[i + 6] = 0.0;
                    target[i + 7] = sin;
                    target[i + 8] = cos;
                    break;

                case 1:
                    target[i + 0] = cos;
                    target[i + 1] = 0.0;
                    target[i + 2] = sin;
                    target[i + 3] = 0.0;
                    target[i + 4] = 1.0;
                    target[i + 5] = 0.0;
                    target[i + 6] = -sin;
                    target[i + 7] = 0.0;
                    target[i + 8] = cos;
                    break;

                default:
                    target[i + 0] = cos;
                    target[i + 1] = -sin;
                    target[i + 2] = 0.0;
                    target[i + 3] = sin;
                    target[i + 4] = cos;
                    target[i + 5] = 0.0;
                    target[i + 6] = 0.0;
                    target[i + 7] = 0.0;
                    target[i + 8] = 1.0;
                    break;
            }
        }

        private void ComputeBind()
        {
            for (var bone = 0; bone < BoneCount; bone++)
            {
                // Lokal: Translation in den Knochenursprung (Wurzel inklusive
                // Hufthoehe), ohne Ruhe-Rotation.
                SetIdentity(_worldRotation, _worldTranslation, bone);
                _worldTranslation[(bone * 3) + 0] = OffsetX[bone];
                _worldTranslation[(bone * 3) + 1] = OffsetY[bone];
                _worldTranslation[(bone * 3) + 2] = OffsetZ[bone];

                if (Parent[bone] >= 0)
                {
                    Multiply(
                        _worldRotation, _worldTranslation, bone,
                        _worldRotation, _worldTranslation, Parent[bone],
                        _worldRotation, _worldTranslation, bone);
                }

                // Inverse der Bind-Weltmatrix (rigid): R^T und -R^T t.
                var wi = bone * 9;
                var ii = bone * 9;

                for (var row = 0; row < 3; row++)
                {
                    for (var column = 0; column < 3; column++)
                    {
                        _bindInverseRotation[ii + (row * 3) + column] = _worldRotation[wi + (column * 3) + row];
                    }
                }

                var wx = _worldTranslation[(bone * 3) + 0];
                var wy = _worldTranslation[(bone * 3) + 1];
                var wz = _worldTranslation[(bone * 3) + 2];

                for (var row = 0; row < 3; row++)
                {
                    _bindInverseTranslation[(bone * 3) + row] =
                        -(_bindInverseRotation[ii + (row * 3) + 0] * wx
                        + _bindInverseRotation[ii + (row * 3) + 1] * wy
                        + _bindInverseRotation[ii + (row * 3) + 2] * wz);
                }
            }
        }

        /// <summary>Bewertet die Pose eines Ticks und fuellt die Palettezeile.</summary>
        public void EvaluateRow(uint unitSeed, double walkPhase, Span<float> paletteRow)
        {
            if (paletteRow.Length != BoneCount * 3 * 4)
            {
                throw new ArgumentException("Palettezeile benoetigt 144 RGBA-Texel.", nameof(paletteRow));
            }

            var phaseJitter = (unitSeed % 97u) * 0.017;

            for (var bone = 0; bone < BoneCount; bone++)
            {
                var parent = Parent[bone];

                SetIdentity(_scratchRotation, _scratchTranslation, bone);
                _scratchTranslation[(bone * 3) + 0] = OffsetX[bone];
                _scratchTranslation[(bone * 3) + 1] = OffsetY[bone];
                _scratchTranslation[(bone * 3) + 2] = OffsetZ[bone];

                if (parent >= 0)
                {
                    Multiply(
                        _scratchRotation, _scratchTranslation, bone,
                        _scratchRotation, _scratchTranslation, parent,
                        _scratchRotation, _scratchTranslation, bone);
                }

                // Prozedurale Gelenkrotation um die feste Achse des Knochens.
                var angle = Amplitude[bone]
                    * Math.Sin((walkPhase * (1.0 + ((bone % 5) * 0.02)))
                    + PhaseOffset[bone]
                    + phaseJitter);

                RotationMatrix(Axis[bone], angle, _jointRotation, 0);

                Multiply(
                    _scratchRotation, _scratchTranslation, bone,
                    _scratchRotation, _scratchTranslation, bone,
                    _jointRotation, ZeroTranslation, 0);
                Multiply(
                    _scratchRotation, _scratchTranslation, bone,
                    _scratchRotation, _scratchTranslation, bone,
                    _bindInverseRotation, _bindInverseTranslation, bone);
            }

            WriteRow(_scratchRotation, paletteRow);
        }

        private static void WriteRow(double[] skinRotation, Span<float> paletteRow)
        {
            for (var bone = 0; bone < BoneCount; bone++)
            {
                var source = bone * 9;
                var target = bone * 12;

                for (var column = 0; column < 3; column++)
                {
                    paletteRow[target + (column * 4) + 0] = (float)skinRotation[source + (0 * 3) + column];
                    paletteRow[target + (column * 4) + 1] = (float)skinRotation[source + (1 * 3) + column];
                    paletteRow[target + (column * 4) + 2] = (float)skinRotation[source + (2 * 3) + column];
                    paletteRow[target + (column * 4) + 3] = 0f;
                }
            }
        }
    }
}

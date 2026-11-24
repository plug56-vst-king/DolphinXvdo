// Biquad Filter Engine (Audio EQ Cookbook implementation)
// Provides exact transfer functions matching Equalizer APO's IIR filters

const FS = 48000; // Standard 48kHz sample rate

class BiquadFilter {
    constructor(type, fc, gain, q) {
        this.type = type || 'PK'; // PK, LS, HS
        this.fc = Math.max(10, Math.min(22000, fc || 1000));
        this.gain = gain || 0; // in dB
        this.q = Math.max(0.1, Math.min(20, q || 1.41));
        this.enabled = true;
        this.calculateCoefficients();
    }

    calculateCoefficients() {
        const w0 = 2 * Math.PI * this.fc / FS;
        const cosW0 = Math.cos(w0);
        const sinW0 = Math.sin(w0);
        const A = Math.pow(10, this.gain / 40);
        const alpha = sinW0 / (2 * this.q);

        let b0, b1, b2, a0, a1, a2;

        switch (this.type) {
            case 'LS': // Low Shelf
                {
                    const twoSqrtAAlpha = 2 * Math.sqrt(A) * alpha;
                    b0 = A * ((A + 1) - (A - 1) * cosW0 + twoSqrtAAlpha);
                    b1 = 2 * A * ((A - 1) - (A + 1) * cosW0);
                    b2 = A * ((A + 1) - (A - 1) * cosW0 - twoSqrtAAlpha);
                    a0 = (A + 1) + (A - 1) * cosW0 + twoSqrtAAlpha;
                    a1 = -2 * ((A - 1) + (A + 1) * cosW0);
                    a2 = (A + 1) - (A - 1) * cosW0 - twoSqrtAAlpha;
                }
                break;
            case 'HS': // High Shelf
                {
                    const twoSqrtAAlpha = 2 * Math.sqrt(A) * alpha;
                    b0 = A * ((A + 1) + (A - 1) * cosW0 + twoSqrtAAlpha);
                    b1 = -2 * A * ((A - 1) + (A + 1) * cosW0);
                    b2 = A * ((A + 1) - (A - 1) * cosW0 - twoSqrtAAlpha);
                    a0 = (A + 1) - (A - 1) * cosW0 + twoSqrtAAlpha;
                    a1 = 2 * ((A - 1) - (A + 1) * cosW0);
                    a2 = (A + 1) - (A - 1) * cosW0 - twoSqrtAAlpha;
                }
                break;
            case 'PK': // Peaking EQ
            default:
                b0 = 1 + alpha * A;
                b1 = -2 * cosW0;
                b2 = 1 - alpha * A;
                a0 = 1 + alpha / A;
                a1 = -2 * cosW0;
                a2 = 1 - alpha / A;
                break;
        }

        // Normalize by a0
        this.b0 = b0 / a0;
        this.b1 = b1 / a0;
        this.b2 = b2 / a0;
        this.a1 = a1 / a0;
        this.a2 = a2 / a0;
    }

    getMagnitudeDb(f) {
        if (!this.enabled || Math.abs(this.gain) < 0.01) return 0;
        const w = 2 * Math.PI * f / FS;
        const cosW = Math.cos(w);
        const sinW = Math.sin(w);
        const cos2W = Math.cos(2 * w);
        const sin2W = Math.sin(2 * w);

        const numRe = this.b0 + this.b1 * cosW + this.b2 * cos2W;
        const numIm = -this.b1 * sinW - this.b2 * sin2W;
        const denRe = 1 + this.a1 * cosW + this.a2 * cos2W;
        const denIm = -this.a1 * sinW - this.a2 * sin2W;

        const numSq = numRe * numRe + numIm * numIm;
        const denSq = denRe * denRe + denIm * denIm;

        if (denSq === 0) return 0;
        return 10 * Math.log10(numSq / denSq);
    }
}

// Compute frequency response curve across logarithmic frequency spectrum
function computeResponseCurve(filters, preamp, numPoints = 400) {
    const minF = 20;
    const maxF = 20000;
    const logMin = Math.log10(minF);
    const logMax = Math.log10(maxF);

    const points = [];
    for (let i = 0; i < numPoints; i++) {
        const ratio = i / (numPoints - 1);
        const f = Math.pow(10, logMin + ratio * (logMax - logMin));
        let totalGain = preamp;

        for (const filter of filters) {
            totalGain += filter.getMagnitudeDb(f);
        }

        points.push({ f, gain: totalGain, xRatio: ratio });
    }
    return points;
}

if (typeof module !== 'undefined') {
    module.exports = { BiquadFilter, computeResponseCurve };
}

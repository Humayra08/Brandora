(() => {
    const form = document.querySelector('#withdrawal-form');
    if (!form) return;
    const input = form.querySelector('#withdrawal-amount');
    const maxButton = form.querySelector('#withdrawal-max');
    if (!input || input.disabled) return;

    const balanceCents = Math.round(Number(form.dataset.balance) * 100);
    const feePercent = Number(form.dataset.fee);
    // Mirrors WalletService.QuoteWithdrawal on the server (which is what actually
    // decides the fee): earnings are split into tranches, each at the rate locked on its
    // payment (cheapest first); the fee is cumulative across all withdrawals, rounded
    // down to the whole taka, and never more than the amount requested.
    const toCents = value => Math.round((Number(value) || 0) * 100);
    let tranches = [];
    try { tranches = JSON.parse(form.dataset.feeTranches || '[]').map(t => [toCents(t[0]), Number(t[1]) || 0]); } catch { tranches = []; }
    const withdrawnCents = toCents(form.dataset.feeWithdrawn);
    const chargedCents = toCents(form.dataset.feeCharged);
    const totalFeeCentsAfter = cumulativeCents => {
        let remaining = cumulativeCents, feeTimes100 = 0; // sum of cents x rate%
        for (const [amountCents, rate] of tranches) {
            if (remaining <= 0) break;
            const taken = Math.min(remaining, amountCents);
            feeTimes100 += taken * rate;
            remaining -= taken;
        }
        return Math.floor(feeTimes100 / 10000 + 1e-9) * 100;
    };
    const feeFor = cents => Math.min(Math.max(0, totalFeeCentsAfter(withdrawnCents + cents) - chargedCents), cents);
    const money = cents => '৳' + (cents / 100).toLocaleString('en-US', { minimumFractionDigits: cents % 100 ? 2 : 0, maximumFractionDigits: 2 });
    const error = form.querySelector('#withdrawal-error');

    function update() {
        const amount = input.valueAsNumber;
        const valid = Number.isFinite(amount) && input.validity.valid;
        const cents = valid ? Math.round(amount * 100) : 0;
        const fee = feeFor(cents);
        document.querySelector('#withdrawal-gross').textContent = valid ? money(cents) : '—';
        document.querySelector('#withdrawal-fee').textContent = valid ? '-' + money(fee) : '—';
        document.querySelector('#withdrawal-net').textContent = valid ? money(cents - fee) : '—';
        error.textContent = valid ? '' : !input.value ? '' :
            'Enter an amount between ' + money(Number(input.min) * 100) + ' and ' + money(balanceCents) + ', with up to two decimal places.';
        return { valid, cents, fee };
    }

    input.addEventListener('input', update);
    maxButton?.addEventListener('click', () => {
        input.value = (balanceCents / 100).toString();
        update();
        input.focus();
    });
    form.addEventListener('submit', event => {
        const state = update();
        if (!form.reportValidity() || !state.valid) event.preventDefault();
    });
    update();
})();

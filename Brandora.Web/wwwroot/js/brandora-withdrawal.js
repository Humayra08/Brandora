(() => {
    const form = document.querySelector('#withdrawal-form');
    if (!form) return;
    // This is a design preview only. There is deliberately no payout POST or API call.
    form.addEventListener('submit', event => event.preventDefault());
    if (form.dataset.preview !== 'true') return;

    const input = form.querySelector('#withdrawal-amount');
    const balanceCents = Math.round(Number(form.dataset.balance) * 100);
    const feePercent = Number(form.dataset.fee);
    const money = cents => '৳' + (cents / 100).toLocaleString('en-US', { minimumFractionDigits: cents % 100 ? 2 : 0, maximumFractionDigits: 2 });
    const error = form.querySelector('#withdrawal-error');
    function update() {
        const amount = input.valueAsNumber;
        const valid = Number.isFinite(amount) && input.validity.valid;
        const cents = valid ? Math.round(amount * 100) : 0;
        const fee = Math.round(cents * feePercent / 100);
        document.querySelector('#withdrawal-gross').textContent = valid ? money(cents) : '—';
        document.querySelector('#withdrawal-fee').textContent = valid ? '-' + money(fee) : '—';
        document.querySelector('#withdrawal-net').textContent = valid ? money(cents - fee) : '—';
        error.textContent = valid ? '' : !input.value ? 'Enter an amount to preview your withdrawal.' :
            'Enter an amount between ' + money(Number(input.min) * 100) + ' and ' + money(balanceCents) + ', with up to two decimal places.';
        return { valid, cents, fee };
    }
    input.addEventListener('input', update);
    document.querySelector('#withdrawal-max').addEventListener('click', () => {
        input.value = (balanceCents / 100).toString();
        update();
        input.focus();
    });
    document.querySelector('#withdrawal-confirm').addEventListener('click', () => {
        const state = update();
        if (!form.reportValidity() || !state.valid) return;
        const method = form.querySelector('input[name="method"]:checked').value;
        document.querySelector('#withdrawal-confirm-message').textContent =
            'In this example, you would receive ' + money(state.cents - state.fee) + ' via ' + method +
            ' after a ' + money(state.fee) + ' example platform fee.';
        document.querySelector('#withdrawal-confirm-dialog').showModal();
    });
    document.querySelector('#withdrawal-history-open').addEventListener('click', () => {
        document.querySelector('#withdrawal-history-dialog').showModal();
    });
    update();
})();
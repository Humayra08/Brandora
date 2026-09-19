(() => {
    const form = document.querySelector('#withdrawal-form');
    if (!form) return;
    const input = form.querySelector('#withdrawal-amount');
    const maxButton = form.querySelector('#withdrawal-max');
    if (!input || input.disabled) return;

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

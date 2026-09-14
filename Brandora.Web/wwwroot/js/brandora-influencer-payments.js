(() => {
    const page = document.querySelector('.payments-page');
    if (!page) return;

    page.querySelectorAll('.payments-filters select').forEach(select => {
        select.addEventListener('change', () => select.form.requestSubmit());
    });
})();

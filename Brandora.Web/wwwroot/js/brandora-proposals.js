(() => {
    const page = document.querySelector('.proposals-page');
    if (!page) return;
    const sort = page.querySelector('#proposal-sort');
    if (sort) {
        sort.form.querySelector('button').hidden = true;
        sort.addEventListener('change', () => sort.form.requestSubmit());
    }
    const notifications = page.querySelector('.proposals-notifications');
    document.addEventListener('click', event => {
        if (notifications && !notifications.contains(event.target)) notifications.open = false;
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && notifications?.open) {
            notifications.open = false;
            notifications.querySelector('summary').focus();
        }
    });
    page.querySelectorAll('.proposal-thumbnail img').forEach(img => {
        const fallback = () => { img.hidden = true; img.parentElement.classList.add('image-unavailable'); };
        img.addEventListener('error', fallback);
        if (img.complete && !img.naturalWidth) fallback();
    });
})();
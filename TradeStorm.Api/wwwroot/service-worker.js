self.addEventListener('push', function (event) {
    const data = event.data ? event.data.json() : { title: 'Loop Market', body: 'Нове сповіщення' };

    const options = {
        body: data.body,
        icon: '/images/logo.png', // Убедись, что иконка существует, либо замени на свою
        badge: '/images/logo.png'
    };

    event.waitUntil(
        self.registration.showNotification(data.title, options)
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    event.waitUntil(
        clients.openWindow('/messages.html')
    );
});
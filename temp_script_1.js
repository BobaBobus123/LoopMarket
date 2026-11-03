const savedTheme = localStorage.getItem('loopTheme') || 'light';
                document.documentElement.setAttribute('data-theme', savedTheme);
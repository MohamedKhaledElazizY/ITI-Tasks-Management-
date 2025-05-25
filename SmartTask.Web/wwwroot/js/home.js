document.addEventListener('DOMContentLoaded', function () {
    // Observer options for intersection observer
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    // Show more projects functionality
    const showMoreProjectsBtn = document.getElementById('show-more-projects');
    const additionalProjects = document.getElementById('additional-projects');
    if (showMoreProjectsBtn && additionalProjects) {
        let projectsExpanded = false;


        showMoreProjectsBtn.addEventListener('click', function () {
            const isHidden = additionalProjects.classList.contains('hidden');

            if (isHidden) {
                additionalProjects.classList.remove('hidden');
                showMoreProjectsBtn.innerHTML = '<span>Show Less Projects</span><span style="margin-left: 0.5rem;">▲</span>';
            } else {
                additionalProjects.classList.add('hidden');
                const projectCount = additionalProjects.getAttribute('data-project-count');
                showMoreProjectsBtn.innerHTML = `<span>Show More Projects (${projectCount})</span><span style="margin-left: 0.5rem;">▼</span>`;
            }
        });
    }

    // Show more progress tasks functionality
    const showMoreBtn = document.getElementById('show-more-progress-tasks');
    const additionalTasks = document.getElementById('additional-progress-tasks');

    if (showMoreBtn && additionalTasks) {
        showMoreBtn.addEventListener('click', function () {
            const isHidden = additionalTasks.classList.contains('hidden');

            if (isHidden) {
                additionalTasks.classList.remove('hidden');
                showMoreBtn.innerHTML = '<span>Show Less Tasks</span><span style="margin-left: 0.5rem;">▲</span>';
            } else {
                additionalTasks.classList.add('hidden');
                const taskCount = additionalTasks.querySelectorAll('.task-card').length;
                showMoreBtn.innerHTML = `<span>Show More Tasks (${taskCount})</span><span style="margin-left: 0.5rem;">▼</span>`;
            }
        });
    }

    // Show more upcoming tasks functionality
    const showMoreUpcomingBtn = document.getElementById('show-more-upcoming-tasks');
    const additionalUpcomingTasks = document.getElementById('additional-upcoming-tasks');

    if (showMoreUpcomingBtn && additionalUpcomingTasks) {


        showMoreUpcomingBtn.addEventListener('click', function () {
            const isHidden = additionalUpcomingTasks.classList.contains('hidden');

            if (isHidden) {
                additionalUpcomingTasks.classList.remove('hidden');
                showMoreUpcomingBtn.innerHTML = '<span>Show Less Tasks</span><span style="margin-left: 0.5rem;">▲</span>';
            } else {
                additionalUpcomingTasks.classList.add('hidden');
                const taskCount = additionalUpcomingTasks.querySelectorAll('.upcoming-card').length;;
                showMoreUpcomingBtn.innerHTML = `<span>Show More Tasks (${taskCount})</span><span style="margin-left: 0.5rem;">▼</span>`;
            }
        });
    }

    // Project overview animation setup
    document.querySelectorAll('.project-overview').forEach(card => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        card.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
    });

    // Intersection observer for general card animations
    const cardObserver = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    // Observe all cards for animation
    document.querySelectorAll(' .project-overview').forEach(card => {
        cardObserver.observe(card);
    });

    // Progress bar animation observer
    const progressObserver = new IntersectionObserver(function (entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const progressBars = entry.target.querySelectorAll('.progress-fill');
                progressBars.forEach(bar => {
                    const width = bar.style.width;
                    bar.style.width = '0%';
                    setTimeout(() => {
                        bar.style.width = width;
                    }, 200);
                });
            }
        });
    }, { threshold: 0.5, rootMargin: '0px 0px -50px 0px' });

    // Observe task cards for progress bar animations
    document.querySelectorAll('.task-card').forEach(card => {
        progressObserver.observe(card);
    });

    // Animate stat numbers
    const statNumbers = document.querySelectorAll('.stat-number');
    const statObserver = new IntersectionObserver(function (entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const target = entry.target;
                const finalNumber = target.textContent;
                const isPercentage = finalNumber.includes('%');
                const numericValue = parseInt(finalNumber);

                if (!isNaN(numericValue)) {
                    let current = 0;
                    const increment = numericValue / 20;
                    const timer = setInterval(() => {
                        current += increment;
                        if (current >= numericValue) {
                            current = numericValue;
                            clearInterval(timer);
                        }
                        target.textContent = Math.floor(current) + (isPercentage ? '%' : '');
                    }, 50);
                }
            }
        });
    }, observerOptions);

    // Observe stat numbers for animation
    statNumbers.forEach(stat => {
        statObserver.observe(stat);
    });

    // Initial progress bar animation on page load
    setTimeout(() => {
        const progressBars = document.querySelectorAll('.progress-fill');
        progressBars.forEach(bar => {
            const width = bar.style.width;
            bar.style.width = '0%';
            setTimeout(() => {
                bar.style.width = width;
            }, 100);
        });
    }, 500);
});
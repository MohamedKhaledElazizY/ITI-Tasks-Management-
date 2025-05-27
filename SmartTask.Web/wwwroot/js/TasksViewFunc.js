
	// Handle opening the popup and loading partial view
	$(document).on('click', '.openPopup', function () {
		const taskId = $(this).data("task-id");

		$.ajax({
			url: '/Task/LoadNodes/' + taskId, // Your action for loading dependencies form
			type: 'GET',
			success: function (response) {
				$('#popupContent').html(response);
				$('#SelectedTaskId').val(taskId);
				$('#popupModal').modal('show');
			},
			error: function () {
				alert('Failed to load task dependencies.');
			}
		});
	});

	// Handle form submission
$(document).on('submit', '#dependencyForm', function (e) {
		e.preventDefault();
		const formData = $(this).serialize();

		$.ajax({
			url: '/Task/SaveSelectedTasks', // Your POST action
			type: 'POST',
			data: formData,
			success: function (e) {
				alert(e);
				$('#popupModal').modal('hide');
			},
			error: function () {
				alert('Failed to save dependencies.');
			}
		});
	});
	function loadTaskDetails(taskId) {
			$.ajax({
				url: '/Task/Details/' + taskId,
				type: 'GET',
				success: function(response) {
					$('#taskDetailsContent').html(response);
					var detailsModal = new bootstrap.Modal(document.getElementById('taskDetailsModal'));
					detailsModal.show();
				},
				error: function(error) {
					console.log('Error loading task details:', error);
					alert('Failed to load task details. Please try again.');
				}
			});
		}

	function numofdepend(taskId) {
		document.querySelector('#condelte').dataset.id = taskId;
		$.ajax({
					url: '/Task/Depend',
			type: 'GET',
			data: { taskid: taskId },
			success: function(x) {
							document.getElementById('depend').innerText='this task has '+x+' Tasks depends on it';
						},
			error: function(xhr) {
							alert('Error: ' + xhr.responseText);
						}
					});
				}
	function deleteTask(b) {
		  var a=  b.dataset.id;
		$.ajax({
					url: '/Task/DeleteTask',
			type: 'DELETE',
				data: { taskid: a },
			success: function() {
									document.getElementById(`row ${a}`).remove();
						},
			error: function(xhr) {
							alert('Error: ' + xhr.responseText);
						}
					});
	}
	function IncreaseStatus(taskId) {
					$.ajax({
				url: '/task/IncreaseStatus/' + taskId,
				type: 'GET',
				success: function(response) {
					$('#status-'+taskId).html(response);
				},
				error: function(xhr) {
					const errorMsg = xhr.responseJSON?.message || "Failed to increase task status. Please try again.";
					alert(errorMsg);
				}
			});
	}

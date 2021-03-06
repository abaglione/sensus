
function scroll_to_class(element_class, removed_height) {
	var scroll_to = $(element_class).offset().top - removed_height;
	if ($(window).scrollTop() != scroll_to) {
		$('html, body').stop().animate({ scrollTop: scroll_to }, 0);
	}
}

function bar_progress(progress_line_object, direction) {
	var number_of_steps = progress_line_object.data('number-of-steps');
	var now_value = progress_line_object.data('now-value');
	var new_value = 0;
	if (direction == 'right') {
		new_value = now_value + (100 / number_of_steps);
	}
	else if (direction == 'left') {
		new_value = now_value - (100 / number_of_steps);
	}
	progress_line_object.attr('style', 'width: ' + new_value + '%;').data('now-value', new_value);
}

jQuery(document).ready(function () {

	var script_uniqueid = 0;

	/*
	  Fullscreen background
	*/
	$.backstretch("assets/img/backgrounds/1.jpg");

	$('#top-navbar-1').on('shown.bs.collapse', function () {
		$.backstretch("resize");
	});
	$('#top-navbar-1').on('hidden.bs.collapse', function () {
		$.backstretch("resize");
	});

	/*
	  Form
	*/
	$('.f1 fieldset:first').fadeIn('slow');

	$('.f1 input[type="text"], .f1 input[type="password"], .f1 textarea').on('focus', function () {
		$(this).removeClass('input-error');
	});

	// next step
	$('.f1 .btn-next').on('click', function () {
		var parent_fieldset = $(this).parents('fieldset');
		var next_step = true;
		// navigation steps / progress steps
		var current_active_step = $(this).parents('.f1').find('.f1-step.active');
		var progress_line = $(this).parents('.f1').find('.f1-progress-line');

		// fields validation
		parent_fieldset.find('input[type="text"], input[type="password"], textarea').each(function () {
			if ($(this).val() == "" && $(this).prop('required')) {
				$(this).addClass('input-error');
				next_step = false;
			}
			else {
				$(this).removeClass('input-error');
			}
		});
		// fields validation

		if (next_step) {
			parent_fieldset.fadeOut(400, function () {
				// change icons
				current_active_step.removeClass('active').addClass('activated').next().addClass('active');
				// progress bar
				bar_progress(progress_line, 'right');
				// show next step
				$(this).next().fadeIn();
				// scroll window to beginning of the form
				scroll_to_class($('.f1'), 20);
			});
		}

	});

	// previous step
	$('.f1 .btn-previous').on('click', function () {
		// navigation steps / progress steps
		var current_active_step = $(this).parents('.f1').find('.f1-step.active');
		var progress_line = $(this).parents('.f1').find('.f1-progress-line');

		$(this).parents('fieldset').fadeOut(400, function () {
			// change icons
			current_active_step.removeClass('active').prev().removeClass('activated').addClass('active');
			// progress bar
			bar_progress(progress_line, 'left');
			// show previous step
			$(this).prev().fadeIn();
			// scroll window to beginning of the form
			scroll_to_class($('.f1'), 20);
		});
	});

	$(".probe_div").hide();

	$(document).on('change', '.probe', function () {
		if (!this.checked) {
			$("#" + this.name + "_probe_div").hide(100);

		} else {
			$("#" + this.name + "_probe_div").show(100);

		}

	});

	$(document).on('click', '.new-script', function () {
		var clone = $('#new-script-template').children().clone();
		var $new_script_html = $(clone);
		var $script_name_edit = $new_script_html.find('.script-name-edit');
		var $new_script_details_div = $new_script_html.find('#_details_div');
		
		var script_name = 'New Script ' + script_uniqueid;
		var current_id = $new_script_details_div.attr("id");

		$new_script_details_div.attr('id', 'script-' + script_uniqueid + current_id)
		
		$script_name_edit.val(script_name);
		
		// Find all of the new script's relevant DOM elements. For each of these,
		// prepend the new script's unique id to the existing name. 
		$new_script_html.find('.script-uniqueid').each(function (index) {
			var current_name = $(this).attr('name');
			$(this).attr('name', 'script_' + script_uniqueid + current_name);
		});

		$new_script_html.appendTo('#scripted-interactions_scripts');
		script_uniqueid += 1;
	});

	$(document).on('change', '.script-details-toggle', function () {
		if (!this.checked) {
			$(this).closest('.script').find('.script-details').hide(100);
		} else {
			$(this).closest('.script').find('.script-details').show(100);
		}
	});

	$(document).on('change', '.start-immediately', function () {

		if (this.checked) {
			$("#start-date-time-div").hide(100);
		}
		else {
			$("#start-date-time-div").show(100);
		}
	});

	$(document).on('change', '.continue-indefinitely', function () {

		if (this.checked) {
			$("#end-date-time-div").hide(100);
		}
		else {
			$("#end-date-time-div").show(100);
		}
	});

	$(document).on('click', '.help', function (e) {

		e.preventDefault();

		window.open('https://predictive-technology-laboratory.github.io/sensus/api/' + this.id, 'help').focus();

	});

	$(document).on('change', '#remote_storage', function () {

		if ($("#remote_storage").val() == "None") {

			$("#s3_div").hide(100);
			$("#console_div").hide(100);

		} else {
			if ($("#remote_storage").val() == "Amazon S3") {
				$("#s3_div").show(100);
				$("#console_div").hide(100);
			} else {
				$("#console_div").show(100);
				$("#s3_div").hide(100);
			}


		}

	});

	$(document).on('change', '#local_storage', function () {

		if ($("#local_storage").val() == "None") {
			$("#local_storage_div").hide(100);
		} else {
			$("#local_storage_div").show(100);
		}

	});


	// submit
	$('#protocol_form').on('submit', function (e) {
		//alert($('#study_description').val());
		e.preventDefault();


		$.post("protocolParser.php", $("#protocol_form").serialize(), function (data) {

			var url = data["res"];
			var name = data["name"];
			$(".qr-code").attr("src", "https://chart.googleapis.com/chart?cht=qr&chl=" + url + "&chs=160x160&chld=L|0");

		});
	});
});

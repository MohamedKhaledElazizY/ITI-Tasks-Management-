﻿using SmartTask.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using TaskModel = SmartTask.Core.Models.Task;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Core.Models.Enums;

namespace SmartTask.Web.ViewModels
{
    public class TaskViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Project is required.")]
        public int ProjectId { get; set; }
        public int? ParentTaskId { get; set; }

        [StringLength(255)]
        [Required(ErrorMessage = "Title is required.")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; }

        [Display(Name = "Start Date")]
        [Remote("ValidateStartDate", "Task", AdditionalFields = "ProjectId", 
        ErrorMessage = "Start date must be within the project timeline.")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [Remote("ValidateEndDate", "Task", AdditionalFields = "ProjectId,StartDate", 
        ErrorMessage = "End date must be within the project timeline and after the start date.")]
        public DateTime? EndDate { get; set; }

    
        public Status Status { get; set; }

        
        public Priority Priority { get; set; }

        public string? CreatedById { get; set; }
        public string? UpdatedById { get; set; }

        [Required]
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }
        [Display(Name = "Updated At")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
    
        public virtual string? ProjectName { get; set; }


        [Display(Name ="Parent Task")]
        public virtual TaskModel? ParentTask { get; set; }

        [Display(Name ="Created By")]
        public virtual ApplicationUser? CreatedBy { get; set; }
        [Display(Name ="Updated By")]
        public virtual ApplicationUser? UpdatedBy { get; set; }

        public virtual ICollection<TaskModel>? SubTasks { get; set; }= new List<TaskModel>();
        //public virtual ICollection<TaskDependency> PredecessorDependencies { get; set; }
        //public virtual ICollection<TaskDependency> SuccessorDependencies { get; set; }
        //public virtual ICollection<Comment> Comments { get; set; }
        //public virtual ICollection<Attachment> Attachments { get; set; }
        //public virtual ICollection<Event> Events { get; set; }
        //public virtual ICollection<AISuggestion> AISuggestions { get; set; }
        public virtual List<string> AssignedToId { get; set; }=new List<string>();

    }
}

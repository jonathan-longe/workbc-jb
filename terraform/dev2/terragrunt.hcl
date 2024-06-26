terraform {
  source = "./"
}

include {
  path = find_in_parent_folders()
}

locals {
  project = get_env("LICENSE_PLATE")
}

generate "dev_tfvars" {
  path              = "dev2.auto.tfvars"
  if_exists         = "overwrite"
  disable_signature = true
  contents          = <<-EOF
    alb_name = "default"
    cloudfront = true
    cloudfront_origin_domain = "workbc-jb-noc.${local.project}-dev.nimbus.cloud.gov.bc.ca"
	cloudfront_origin_domain2 = "workbc-jb-noc-adm.${local.project}-dev.nimbus.cloud.gov.bc.ca"
    service_names = ["workbc-jb-noc"]
	service_names2 = ["workbc-jb-noc-adm"]
  EOF
}
